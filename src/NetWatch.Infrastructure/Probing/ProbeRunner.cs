using Microsoft.Extensions.Logging;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Common.Models;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Infrastructure.Probing;

/// <summary>
/// Routes a probe to the executor registered for its type and guarantees the scheduler
/// always gets a result back.
///
/// The catch-all is deliberate: one executor throwing an unexpected exception must not
/// tear down the monitoring loop and stop every other check in the system.
/// </summary>
public class ProbeRunner : IProbeRunner
{
    private readonly Dictionary<ProbeType, IProbeExecutor> _executors;
    private readonly ILogger<ProbeRunner> _logger;

    public ProbeRunner(IEnumerable<IProbeExecutor> executors, ILogger<ProbeRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(executors);

        _executors = executors.ToDictionary(e => e.Type);
        _logger = logger;
    }

    public async Task<ProbeExecutionResult> RunAsync(Probe probe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);

        if (!_executors.TryGetValue(probe.Type, out var executor))
        {
            return ProbeExecutionResult.Failure(ProbeOutcome.Error, $"No executor registered for probe type {probe.Type}.");
        }

        try
        {
            return await executor.ExecuteAsync(probe, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Application shutdown — let the scheduler unwind rather than recording a
            // fake outage for every probe in flight.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Probe {ProbeId} ({ProbeType}) threw an unhandled exception.", probe.Id, probe.Type);
            return ProbeExecutionResult.Failure(ProbeOutcome.Error, $"Probe failed unexpectedly: {ex.Message}");
        }
    }
}
