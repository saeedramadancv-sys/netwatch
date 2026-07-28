using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetWatch.Application.Common.Interfaces;

namespace NetWatch.Infrastructure.Monitoring;

/// <summary>
/// Deletes check results older than the retention window.
///
/// Without this the results table grows forever: a hundred probes on a 30-second
/// interval write roughly 100 million rows a year. Deletes run in bounded batches and
/// loop until a batch comes back short, so a first run against a large backlog makes
/// progress without holding a table lock for minutes.
/// </summary>
public class ResultRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<MonitoringOptions> options,
    TimeProvider timeProvider,
    ILogger<ResultRetentionService> logger) : BackgroundService
{
    private readonly MonitoringOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || _options.ResultRetentionDays <= 0)
        {
            logger.LogInformation("Result retention is disabled.");
            return;
        }

        var period = TimeSpan.FromHours(Math.Max(1, _options.RetentionSweepHours));
        using var timer = new PeriodicTimer(period);

        try
        {
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-_options.ResultRetentionDays);
            var batchSize = Math.Max(100, _options.RetentionBatchSize);
            var total = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var results = scope.ServiceProvider.GetRequiredService<IProbeResultRepository>();

                var deleted = await results.DeleteOlderThanAsync(cutoff, batchSize, stoppingToken);
                total += deleted;

                // A short batch means the backlog is drained.
                if (deleted < batchSize)
                {
                    break;
                }
            }

            if (total > 0)
            {
                logger.LogInformation("Retention removed {Count} check results older than {Cutoff:u}.", total, cutoff);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Retention sweep failed. It will run again on the next schedule.");
        }
    }
}
