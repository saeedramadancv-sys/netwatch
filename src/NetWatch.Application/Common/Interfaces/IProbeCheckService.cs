using NetWatch.Domain.Entities;

namespace NetWatch.Application.Common.Interfaces;

/// <summary>
/// Runs one probe end to end: execute, grade, persist, update incidents, notify.
/// Separated from the scheduler so the "what happens on a check" logic can be tested
/// without a hosted service, a timer, or a real network.
/// </summary>
public interface IProbeCheckService
{
    /// <summary>
    /// Executes the probe and commits everything that follows from the result.
    /// The probe must be tracked by the current unit of work so its state change persists.
    /// </summary>
    Task<ProbeTransition> CheckAsync(Probe probe, CancellationToken cancellationToken = default);
}
