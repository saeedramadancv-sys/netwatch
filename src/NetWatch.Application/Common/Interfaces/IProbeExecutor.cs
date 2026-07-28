using NetWatch.Application.Common.Models;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Application.Common.Interfaces;

/// <summary>
/// Performs the actual network call for one probe type.
/// One implementation per <see cref="ProbeType"/>; the runner selects by
/// <see cref="Type"/>, so adding a new check type means adding a class and
/// registering it, with no changes to the scheduler.
/// </summary>
public interface IProbeExecutor
{
    ProbeType Type { get; }

    /// <summary>
    /// Never throws for network conditions — an unreachable host is a normal result,
    /// not an exception. Only <see cref="OperationCanceledException"/> on shutdown.
    /// </summary>
    Task<ProbeExecutionResult> ExecuteAsync(Probe probe, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dispatches a probe to the executor registered for its type.
/// </summary>
public interface IProbeRunner
{
    Task<ProbeExecutionResult> RunAsync(Probe probe, CancellationToken cancellationToken = default);
}
