using NetWatch.Domain.Common;
using NetWatch.Domain.Enums;

namespace NetWatch.Domain.Entities;

/// <summary>
/// One configured check against a <see cref="Device"/>, plus the debounced health
/// state derived from its results.
///
/// The state machine lives here rather than in the scheduler on purpose: it is pure,
/// has no I/O, and can therefore be unit tested exhaustively without a network, a
/// clock, or a database.
/// </summary>
public class Probe : BaseEntity
{
    public const int MinIntervalSeconds = 5;
    public const int MaxIntervalSeconds = 86_400;
    public const int MinTimeoutMs = 100;
    public const int MaxTimeoutMs = 60_000;
    public const int MaxThreshold = 10;

    // EF Core materialisation constructor.
    private Probe()
    {
    }

    public Probe(
        ProbeType type,
        int intervalSeconds = 60,
        int timeoutMs = 5_000,
        int? port = null,
        string? httpPath = null,
        bool useHttps = true,
        int expectedStatusCode = 200,
        int failureThreshold = 3,
        int recoveryThreshold = 2,
        int? degradedLatencyMs = null)
    {
        Type = type;
        IntervalSeconds = Guard.AgainstOutOfRange(intervalSeconds, MinIntervalSeconds, MaxIntervalSeconds, nameof(intervalSeconds));
        TimeoutMs = Guard.AgainstOutOfRange(timeoutMs, MinTimeoutMs, MaxTimeoutMs, nameof(timeoutMs));
        FailureThreshold = Guard.AgainstOutOfRange(failureThreshold, 1, MaxThreshold, nameof(failureThreshold));
        RecoveryThreshold = Guard.AgainstOutOfRange(recoveryThreshold, 1, MaxThreshold, nameof(recoveryThreshold));
        UseHttps = useHttps;
        ExpectedStatusCode = expectedStatusCode;
        DegradedLatencyMs = degradedLatencyMs;
        Port = port;
        HttpPath = httpPath;
        IsEnabled = true;
        State = ProbeState.Unknown;

        ValidateShape();
    }

    public int DeviceId { get; private set; }

    public Device Device { get; private set; } = null!;

    public ProbeType Type { get; private set; }

    /// <summary>Required for <see cref="ProbeType.Tcp"/>; optional for HTTP (falls back to the scheme default).</summary>
    public int? Port { get; private set; }

    /// <summary>Path requested by an HTTP probe, e.g. "/health". Always starts with '/'.</summary>
    public string? HttpPath { get; private set; }

    public bool UseHttps { get; private set; }

    /// <summary>Status code an HTTP probe must receive to count as a success.</summary>
    public int ExpectedStatusCode { get; private set; }

    /// <summary>How often the scheduler runs this check.</summary>
    public int IntervalSeconds { get; private set; }

    public int TimeoutMs { get; private set; }

    /// <summary>Consecutive failures required before the probe is declared down.</summary>
    public int FailureThreshold { get; private set; }

    /// <summary>Consecutive successes required before a down probe is declared healthy again.</summary>
    public int RecoveryThreshold { get; private set; }

    /// <summary>Responses slower than this are classified as degraded. Null disables latency grading.</summary>
    public int? DegradedLatencyMs { get; private set; }

    public bool IsEnabled { get; private set; }

    public ProbeState State { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public int ConsecutiveSuccesses { get; private set; }

    public int ConsecutiveDegraded { get; private set; }

    public DateTime? LastCheckedAtUtc { get; private set; }

    public double? LastResponseTimeMs { get; private set; }

    public ProbeOutcome? LastOutcome { get; private set; }

    /// <summary>
    /// Links this probe to its device. Called by <see cref="Device.AddProbe"/> only —
    /// a probe cannot move between devices, because its history belongs to the host it
    /// was measuring.
    /// </summary>
    internal void AttachTo(Device device)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        DeviceId = device.Id;
    }

    /// <summary>
    /// Human-readable target used by probes and shown in the UI, e.g.
    /// "10.0.0.1", "10.0.0.1:443" or "https://example.com/health".
    /// </summary>
    public string Describe()
    {
        var host = Device?.Hostname ?? "?";
        return Type switch
        {
            ProbeType.Icmp => host,
            ProbeType.Tcp => $"{host}:{Port}",
            ProbeType.Http => BuildHttpUrl(host),
            _ => host
        };
    }

    public string BuildHttpUrl(string host)
    {
        var scheme = UseHttps ? "https" : "http";
        var defaultPort = UseHttps ? 443 : 80;
        var authority = Port is null || Port == defaultPort ? host : $"{host}:{Port}";
        return $"{scheme}://{authority}{HttpPath ?? "/"}";
    }

    /// <summary>
    /// Turns a raw executor outcome into the graded outcome that drives state.
    ///
    /// Latency grading lives here, not in the executors: whether 800ms is "slow"
    /// is a per-probe policy decision, and a socket has no opinion about it.
    /// </summary>
    public ProbeOutcome Grade(ProbeOutcome rawOutcome, double? responseTimeMs)
    {
        if (rawOutcome != ProbeOutcome.Success)
        {
            return rawOutcome;
        }

        if (DegradedLatencyMs is int threshold && responseTimeMs is double elapsed && elapsed > threshold)
        {
            return ProbeOutcome.Degraded;
        }

        return ProbeOutcome.Success;
    }

    /// <summary>
    /// Whether this probe is due to run at <paramref name="nowUtc"/>.
    /// A probe that has never run is always due.
    /// </summary>
    public bool IsDue(DateTime nowUtc) =>
        IsEnabled
        && (LastCheckedAtUtc is null || nowUtc - LastCheckedAtUtc.Value >= TimeSpan.FromSeconds(IntervalSeconds));

    /// <summary>
    /// Applies one check result and returns what changed.
    ///
    /// Debouncing is the whole point: a single dropped ICMP packet must not page
    /// anyone, and a single successful reply must not close a real outage. State only
    /// moves once <see cref="FailureThreshold"/> or <see cref="RecoveryThreshold"/>
    /// consecutive results agree.
    /// </summary>
    public ProbeTransition RecordResult(ProbeOutcome outcome, double? responseTimeMs, DateTime checkedAtUtc)
    {
        var previous = State;

        switch (outcome)
        {
            case ProbeOutcome.Success:
                ConsecutiveSuccesses++;
                ConsecutiveFailures = 0;
                ConsecutiveDegraded = 0;
                break;

            case ProbeOutcome.Degraded:
                // The target answered, so this is not a reachability failure — but it is
                // not healthy either. Tracked on its own counter so a flapping-slow
                // service cannot be mistaken for a hard outage.
                ConsecutiveDegraded++;
                ConsecutiveFailures = 0;
                ConsecutiveSuccesses = 0;
                break;

            default:
                ConsecutiveFailures++;
                ConsecutiveSuccesses = 0;
                ConsecutiveDegraded = 0;
                break;
        }

        var next = previous;

        if (ConsecutiveFailures >= FailureThreshold)
        {
            next = ProbeState.Down;
        }
        else if (ConsecutiveDegraded >= FailureThreshold)
        {
            next = ProbeState.Degraded;
        }
        else if (ConsecutiveSuccesses >= RecoveryThreshold
                 || (previous == ProbeState.Unknown && ConsecutiveSuccesses > 0))
        {
            // A probe with no history is trusted on its first success; there is no
            // prior good state that a false recovery could destroy.
            next = ProbeState.Up;
        }

        State = next;
        LastCheckedAtUtc = checkedAtUtc;
        LastResponseTimeMs = responseTimeMs;
        LastOutcome = outcome;

        var wasProblem = ProbeTransition.IsProblem(previous);
        var isProblem = ProbeTransition.IsProblem(next);

        return new ProbeTransition(
            PreviousState: previous,
            NewState: next,
            ShouldOpenIncident: isProblem && !wasProblem,
            ShouldResolveIncident: wasProblem && !isProblem,
            ShouldEscalateIncident: wasProblem && isProblem && previous == ProbeState.Degraded && next == ProbeState.Down,
            Severity: ProbeTransition.SeverityFor(next));
    }

    public void Update(
        int intervalSeconds,
        int timeoutMs,
        int? port,
        string? httpPath,
        bool useHttps,
        int expectedStatusCode,
        int failureThreshold,
        int recoveryThreshold,
        int? degradedLatencyMs)
    {
        IntervalSeconds = Guard.AgainstOutOfRange(intervalSeconds, MinIntervalSeconds, MaxIntervalSeconds, nameof(intervalSeconds));
        TimeoutMs = Guard.AgainstOutOfRange(timeoutMs, MinTimeoutMs, MaxTimeoutMs, nameof(timeoutMs));
        FailureThreshold = Guard.AgainstOutOfRange(failureThreshold, 1, MaxThreshold, nameof(failureThreshold));
        RecoveryThreshold = Guard.AgainstOutOfRange(recoveryThreshold, 1, MaxThreshold, nameof(recoveryThreshold));
        Port = port;
        HttpPath = httpPath;
        UseHttps = useHttps;
        ExpectedStatusCode = expectedStatusCode;
        DegradedLatencyMs = degradedLatencyMs;
        UpdatedAtUtc = DateTime.UtcNow;

        ValidateShape();
    }

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
        {
            return;
        }

        IsEnabled = enabled;
        UpdatedAtUtc = DateTime.UtcNow;

        if (!enabled)
        {
            // Health of a disabled probe is meaningless; clear it so the dashboard does
            // not keep showing a stale "down" tile for something nobody is checking.
            ResetHealth();
        }
    }

    private void ResetHealth()
    {
        State = ProbeState.Unknown;
        ConsecutiveFailures = 0;
        ConsecutiveSuccesses = 0;
        ConsecutiveDegraded = 0;
        LastOutcome = null;
        LastResponseTimeMs = null;
    }

    /// <summary>
    /// Cross-field rules that only make sense once every property is set.
    /// </summary>
    private void ValidateShape()
    {
        if (TimeoutMs >= IntervalSeconds * 1_000)
        {
            throw new DomainException("Timeout must be shorter than the check interval, otherwise checks overlap.");
        }

        if (DegradedLatencyMs is int degraded)
        {
            Guard.AgainstOutOfRange(degraded, 1, TimeoutMs, nameof(DegradedLatencyMs));
        }

        switch (Type)
        {
            case ProbeType.Tcp:
                if (Port is null)
                {
                    throw new DomainException("A TCP probe requires a port.");
                }

                Guard.AgainstOutOfRange(Port.Value, 1, 65_535, nameof(Port));
                break;

            case ProbeType.Http:
                if (Port is int httpPort)
                {
                    Guard.AgainstOutOfRange(httpPort, 1, 65_535, nameof(Port));
                }

                if (HttpPath is not null)
                {
                    HttpPath = Guard.AgainstTooLong(HttpPath.Trim(), 500, nameof(HttpPath));
                    if (!HttpPath.StartsWith('/'))
                    {
                        throw new DomainException("HTTP path must start with '/'.");
                    }
                }

                Guard.AgainstOutOfRange(ExpectedStatusCode, 100, 599, nameof(ExpectedStatusCode));
                break;

            case ProbeType.Icmp:
                if (Port is not null)
                {
                    throw new DomainException("An ICMP probe cannot target a port.");
                }

                break;
        }
    }
}
