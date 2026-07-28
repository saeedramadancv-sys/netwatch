using NetWatch.Domain.Entities;

namespace NetWatch.Application.Probes;

public static class ProbeMapping
{
    /// <summary>
    /// Projects a probe to its API shape. The device must be loaded — every response
    /// carries the device name so clients never have to make a second call to render a row.
    /// </summary>
    public static ProbeResponse ToResponse(this Probe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        return new ProbeResponse(
            probe.Id,
            probe.DeviceId,
            probe.Device?.Name ?? string.Empty,
            probe.Type,
            probe.Describe(),
            probe.Port,
            probe.HttpPath,
            probe.UseHttps,
            probe.ExpectedStatusCode,
            probe.IntervalSeconds,
            probe.TimeoutMs,
            probe.FailureThreshold,
            probe.RecoveryThreshold,
            probe.DegradedLatencyMs,
            probe.IsEnabled,
            probe.State,
            probe.LastOutcome,
            probe.LastResponseTimeMs,
            probe.LastCheckedAtUtc);
    }

    public static ProbeHistoryPoint ToHistoryPoint(this ProbeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ProbeHistoryPoint(
            result.CheckedAtUtc,
            result.IsSuccess,
            result.Outcome,
            result.ResponseTimeMs,
            result.StatusCode,
            result.ErrorMessage);
    }
}
