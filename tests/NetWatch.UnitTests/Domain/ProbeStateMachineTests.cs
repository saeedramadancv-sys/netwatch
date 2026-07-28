using FluentAssertions;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.UnitTests.Domain;

/// <summary>
/// The debouncing state machine, which is the core of the whole product: it decides when
/// a blip becomes an outage and when an outage is over.
///
/// These tests need no database, no network and no clock — the logic was deliberately kept
/// pure so that every transition can be driven directly.
/// </summary>
public class ProbeStateMachineTests
{
    private static readonly DateTime At = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Probe CreateProbe(int failureThreshold = 3, int recoveryThreshold = 2) =>
        new(ProbeType.Icmp, intervalSeconds: 60, timeoutMs: 5_000, failureThreshold: failureThreshold, recoveryThreshold: recoveryThreshold);

    [Fact]
    public void NewProbe_StartsUnknown()
    {
        var probe = CreateProbe();

        probe.State.Should().Be(ProbeState.Unknown);
        probe.LastCheckedAtUtc.Should().BeNull();
    }

    [Fact]
    public void FirstSuccess_GoesUpImmediately_WithoutWaitingForRecoveryThreshold()
    {
        // A probe with no history has no good state to protect, so one success is enough.
        var probe = CreateProbe(recoveryThreshold: 5);

        var transition = probe.RecordResult(ProbeOutcome.Success, 12, At);

        probe.State.Should().Be(ProbeState.Up);
        transition.NewState.Should().Be(ProbeState.Up);
        transition.StateChanged.Should().BeTrue();
        transition.ShouldOpenIncident.Should().BeFalse();
    }

    [Fact]
    public void SingleFailure_DoesNotTakeAHealthyProbeDown()
    {
        // The entire point of debouncing: one dropped packet is not an outage.
        var probe = CreateProbe(failureThreshold: 3);
        probe.RecordResult(ProbeOutcome.Success, 10, At);

        var transition = probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(1));

        probe.State.Should().Be(ProbeState.Up);
        transition.StateChanged.Should().BeFalse();
        transition.ShouldOpenIncident.Should().BeFalse();
        probe.ConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public void FailuresReachingTheThreshold_GoDownAndOpenAnIncident()
    {
        var probe = CreateProbe(failureThreshold: 3);
        probe.RecordResult(ProbeOutcome.Success, 10, At);

        probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(1));
        probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(2));
        var transition = probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(3));

        probe.State.Should().Be(ProbeState.Down);
        transition.ShouldOpenIncident.Should().BeTrue();
        transition.Severity.Should().Be(IncidentSeverity.Critical);
    }

    [Fact]
    public void StayingDown_DoesNotOpenASecondIncident()
    {
        var probe = CreateProbe(failureThreshold: 1);
        probe.RecordResult(ProbeOutcome.Timeout, null, At);

        var transition = probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(1));

        transition.ShouldOpenIncident.Should().BeFalse();
        transition.StateChanged.Should().BeFalse();
        probe.State.Should().Be(ProbeState.Down);
    }

    [Fact]
    public void SingleSuccess_DoesNotCloseAnOutageWhenRecoveryThresholdIsHigher()
    {
        // A flapping service that answers once must not be reported as recovered.
        var probe = CreateProbe(failureThreshold: 1, recoveryThreshold: 3);
        probe.RecordResult(ProbeOutcome.Timeout, null, At);

        var transition = probe.RecordResult(ProbeOutcome.Success, 10, At.AddMinutes(1));

        probe.State.Should().Be(ProbeState.Down);
        transition.ShouldResolveIncident.Should().BeFalse();
    }

    [Fact]
    public void EnoughConsecutiveSuccesses_RecoverAndResolveTheIncident()
    {
        var probe = CreateProbe(failureThreshold: 1, recoveryThreshold: 2);
        probe.RecordResult(ProbeOutcome.Timeout, null, At);

        probe.RecordResult(ProbeOutcome.Success, 10, At.AddMinutes(1));
        var transition = probe.RecordResult(ProbeOutcome.Success, 11, At.AddMinutes(2));

        probe.State.Should().Be(ProbeState.Up);
        transition.ShouldResolveIncident.Should().BeTrue();
        transition.Severity.Should().BeNull();
    }

    [Fact]
    public void AFailureMidRecovery_RestartsTheRecoveryCount()
    {
        var probe = CreateProbe(failureThreshold: 5, recoveryThreshold: 3);
        probe.RecordResult(ProbeOutcome.Timeout, null, At);
        probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(1));
        probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(2));
        probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(3));
        probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(4));
        probe.State.Should().Be(ProbeState.Down);

        probe.RecordResult(ProbeOutcome.Success, 10, At.AddMinutes(5));
        probe.RecordResult(ProbeOutcome.Success, 10, At.AddMinutes(6));
        probe.ConsecutiveSuccesses.Should().Be(2);

        probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(7));

        probe.ConsecutiveSuccesses.Should().Be(0);
        probe.State.Should().Be(ProbeState.Down);
    }

    [Fact]
    public void SustainedDegradation_OpensAWarningIncidentRatherThanCritical()
    {
        var probe = CreateProbe(failureThreshold: 2);
        probe.RecordResult(ProbeOutcome.Success, 10, At);

        probe.RecordResult(ProbeOutcome.Degraded, 900, At.AddMinutes(1));
        var transition = probe.RecordResult(ProbeOutcome.Degraded, 950, At.AddMinutes(2));

        probe.State.Should().Be(ProbeState.Degraded);
        transition.ShouldOpenIncident.Should().BeTrue();
        transition.Severity.Should().Be(IncidentSeverity.Warning);
    }

    [Fact]
    public void DegradedTurningIntoDown_EscalatesInsteadOfOpeningANewIncident()
    {
        var probe = CreateProbe(failureThreshold: 2);
        probe.RecordResult(ProbeOutcome.Degraded, 900, At);
        probe.RecordResult(ProbeOutcome.Degraded, 900, At.AddMinutes(1));
        probe.State.Should().Be(ProbeState.Degraded);

        probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(2));
        var transition = probe.RecordResult(ProbeOutcome.Timeout, null, At.AddMinutes(3));

        probe.State.Should().Be(ProbeState.Down);
        transition.ShouldEscalateIncident.Should().BeTrue();
        transition.ShouldOpenIncident.Should().BeFalse();
        transition.Severity.Should().Be(IncidentSeverity.Critical);
    }

    [Fact]
    public void RecoveringFromDegraded_ResolvesTheIncident()
    {
        var probe = CreateProbe(failureThreshold: 2, recoveryThreshold: 1);
        probe.RecordResult(ProbeOutcome.Degraded, 900, At);
        probe.RecordResult(ProbeOutcome.Degraded, 900, At.AddMinutes(1));

        var transition = probe.RecordResult(ProbeOutcome.Success, 20, At.AddMinutes(2));

        probe.State.Should().Be(ProbeState.Up);
        transition.ShouldResolveIncident.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProbeOutcome.Timeout)]
    [InlineData(ProbeOutcome.ConnectionRefused)]
    [InlineData(ProbeOutcome.DnsFailure)]
    [InlineData(ProbeOutcome.Unreachable)]
    [InlineData(ProbeOutcome.UnexpectedStatusCode)]
    [InlineData(ProbeOutcome.TlsFailure)]
    [InlineData(ProbeOutcome.Error)]
    public void EveryNonAnsweringOutcome_CountsAsAFailure(ProbeOutcome outcome)
    {
        var probe = CreateProbe(failureThreshold: 1);

        probe.RecordResult(outcome, null, At);

        probe.State.Should().Be(ProbeState.Down);
    }

    [Fact]
    public void RecordingAResult_StoresTheLatestObservation()
    {
        var probe = CreateProbe();

        probe.RecordResult(ProbeOutcome.Success, 42.5, At);

        probe.LastCheckedAtUtc.Should().Be(At);
        probe.LastResponseTimeMs.Should().Be(42.5);
        probe.LastOutcome.Should().Be(ProbeOutcome.Success);
    }

    [Fact]
    public void DisablingAProbe_ClearsItsHealthSoTheDashboardDoesNotShowAStaleTile()
    {
        var probe = CreateProbe(failureThreshold: 1);
        probe.RecordResult(ProbeOutcome.Timeout, null, At);
        probe.State.Should().Be(ProbeState.Down);

        probe.SetEnabled(false);

        probe.State.Should().Be(ProbeState.Unknown);
        probe.ConsecutiveFailures.Should().Be(0);
        probe.LastOutcome.Should().BeNull();
    }
}
