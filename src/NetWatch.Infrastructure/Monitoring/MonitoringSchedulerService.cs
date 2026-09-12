using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetWatch.Application.Common.Interfaces;

namespace NetWatch.Infrastructure.Monitoring;

/// <summary>
/// The monitoring loop. Wakes on a fixed tick, asks the database which probes are due,
/// and runs them concurrently under a hard concurrency cap.
///
/// Three details carry most of the weight here:
///
/// <para><b>No drift.</b> <see cref="PeriodicTimer"/> ticks on a fixed schedule rather
/// than sleeping for a fixed duration after each batch. A <c>Task.Delay</c> loop would
/// add the work time to every cycle, so a 5s loop doing 2s of work would silently
/// become a 7s loop and every probe would run late by a growing margin.</para>
///
/// <para><b>A scope per check.</b> <c>DbContext</c> is not thread safe, so parallel
/// checks cannot share one. Each check resolves its own scope, which also means one
/// failing check cannot poison the change tracker of the others.</para>
///
/// <para><b>Bounded concurrency.</b> A semaphore caps in-flight checks. Without it,
/// a thousand due probes would open a thousand sockets and database connections at
/// once and take the process down instead of the network being monitored.</para>
/// </summary>
public class MonitoringSchedulerService(
    IServiceScopeFactory scopeFactory,
    IOptions<MonitoringOptions> options,
    TimeProvider timeProvider,
    ILogger<MonitoringSchedulerService> logger) : BackgroundService
{
    private readonly MonitoringOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogWarning("Monitoring scheduler is disabled by configuration.");
            return;
        }

        var tick = TimeSpan.FromSeconds(Math.Max(1, _options.TickSeconds));

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Monitoring scheduler started: tick {Tick}s, max {MaxConcurrent} concurrent checks.",
                tick.TotalSeconds, _options.MaxConcurrentChecks);
        }

        using var timer = new PeriodicTimer(tick);
        using var throttle = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentChecks));

        // Run one sweep immediately so a restart does not leave the dashboard blank for
        // a whole tick.
        await RunSweepAsync(throttle, stoppingToken);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunSweepAsync(throttle, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }

        logger.LogInformation("Monitoring scheduler stopped.");
    }

    private async Task RunSweepAsync(SemaphoreSlim throttle, CancellationToken stoppingToken)
    {
        try
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;

            IReadOnlyList<int> dueIds;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var probes = scope.ServiceProvider.GetRequiredService<IProbeRepository>();
                dueIds = await probes.GetDueProbeIdsAsync(now, stoppingToken);
            }

            if (dueIds.Count == 0)
            {
                return;
            }

            var batch = dueIds.Take(_options.MaxChecksPerTick).ToArray();
            if (batch.Length < dueIds.Count)
            {
                logger.LogWarning(
                    "{Due} probes are due but only {Batch} will run this tick. The system is behind; raise MaxChecksPerTick or MaxConcurrentChecks.",
                    dueIds.Count, batch.Length);
            }

            var checks = batch.Select(id => RunOneAsync(id, throttle, stoppingToken));
            await Task.WhenAll(checks);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A sweep must never kill the loop: a transient database outage should pause
            // monitoring, not end it for the lifetime of the process.
            logger.LogError(ex, "Monitoring sweep failed. The scheduler will retry on the next tick.");
        }
    }

    private async Task RunOneAsync(int probeId, SemaphoreSlim throttle, CancellationToken stoppingToken)
    {
        await throttle.WaitAsync(stoppingToken);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var probes = scope.ServiceProvider.GetRequiredService<IProbeRepository>();
            var checker = scope.ServiceProvider.GetRequiredService<IProbeCheckService>();

            var probe = await probes.GetWithDeviceAsync(probeId, stoppingToken);
            if (probe is null)
            {
                // Deleted between the sweep query and now. Nothing to do.
                return;
            }

            await checker.CheckAsync(probe, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown mid-check; the result is simply not recorded.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Check for probe {ProbeId} failed.", probeId);
        }
        finally
        {
            throttle.Release();
        }
    }
}
