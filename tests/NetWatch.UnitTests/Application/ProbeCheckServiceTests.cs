using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Common.Models;
using NetWatch.Application.Monitoring;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;
using NSubstitute;

namespace NetWatch.UnitTests.Application;

/// <summary>
/// The orchestration around a single check: run, grade, record, update incidents, notify.
///
/// Everything external is substituted, so these tests assert the sequencing and the
/// decisions — never the network. FakeTimeProvider supplies a fixed clock so timestamps
/// are exact rather than approximate.
/// </summary>
public class ProbeCheckServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly IProbeRunner _runner = Substitute.For<IProbeRunner>();
    private readonly IProbeResultRepository _results = Substitute.For<IProbeResultRepository>();
    private readonly IIncidentRepository _incidents = Substitute.For<IIncidentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMonitoringNotifier _notifier = Substitute.For<IMonitoringNotifier>();
    private readonly FakeTimeProvider _clock = new(Now);

    private ProbeCheckService CreateService() =>
        new(_runner, _results, _incidents, _unitOfWork, _notifier, _clock, NullLogger<ProbeCheckService>.Instance);

    private static Probe CreateProbe(int failureThreshold = 1, int recoveryThreshold = 1, int? degradedLatencyMs = null)
    {
        var device = new Device("Edge Router", "10.0.0.1", DeviceCategory.Router);
        var probe = new Probe(
            ProbeType.Icmp,
            intervalSeconds: 60,
            timeoutMs: 5_000,
            failureThreshold: failureThreshold,
            recoveryThreshold: recoveryThreshold,
            degradedLatencyMs: degradedLatencyMs);

        device.AddProbe(probe);
        return probe;
    }

    private void GivenProbeReturns(ProbeExecutionResult result) =>
        _runner.RunAsync(Arg.Any<Probe>(), Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task ASuccessfulCheck_RecordsAResultAndCommits()
    {
        GivenProbeReturns(ProbeExecutionResult.Success(12.5));
        var probe = CreateProbe();

        await CreateService().CheckAsync(probe);

        _results.Received(1).Add(Arg.Is<ProbeResult>(r =>
            r.Outcome == ProbeOutcome.Success && r.ResponseTimeMs == 12.5 && r.CheckedAtUtc == Now));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ASlowSuccess_IsGradedAsDegradedUsingTheProbesOwnThreshold()
    {
        // The executor reports a plain success; the grading rule lives on the probe.
        GivenProbeReturns(ProbeExecutionResult.Success(800));
        var probe = CreateProbe(degradedLatencyMs: 200);

        await CreateService().CheckAsync(probe);

        _results.Received(1).Add(Arg.Is<ProbeResult>(r => r.Outcome == ProbeOutcome.Degraded));
    }

    [Fact]
    public async Task CrossingTheFailureThreshold_OpensAnIncident()
    {
        GivenProbeReturns(ProbeExecutionResult.Failure(ProbeOutcome.Timeout, "No reply."));
        _incidents.GetActiveForProbeAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((Incident?)null);
        var probe = CreateProbe(failureThreshold: 1);

        var transition = await CreateService().CheckAsync(probe);

        transition.ShouldOpenIncident.Should().BeTrue();
        _incidents.Received(1).Add(Arg.Is<Incident>(i =>
            i.Severity == IncidentSeverity.Critical && i.Cause == "No reply." && i.StartedAtUtc == Now));
    }

    [Fact]
    public async Task RecoveringResolvesTheOpenIncidentAtTheTimeOfTheSuccessfulCheck()
    {
        var existing = new Incident(1, Now.AddMinutes(-10), IncidentSeverity.Critical, "No reply.");
        _incidents.GetActiveForProbeAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(existing);

        var probe = CreateProbe(failureThreshold: 1, recoveryThreshold: 1);
        GivenProbeReturns(ProbeExecutionResult.Failure(ProbeOutcome.Timeout, "No reply."));
        await CreateService().CheckAsync(probe);

        GivenProbeReturns(ProbeExecutionResult.Success(15));
        var transition = await CreateService().CheckAsync(probe);

        transition.ShouldResolveIncident.Should().BeTrue();
        existing.Status.Should().Be(IncidentStatus.Resolved);
        existing.ResolvedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task AnAlreadyOpenIncidentIsReusedRatherThanDuplicated()
    {
        // Guards the crash-recovery path: an orphaned open incident must not spawn a second
        // record for the same outage.
        var orphan = new Incident(1, Now.AddMinutes(-5), IncidentSeverity.Critical, "Earlier failure.");
        _incidents.GetActiveForProbeAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(orphan);

        GivenProbeReturns(ProbeExecutionResult.Failure(ProbeOutcome.Timeout, "No reply."));
        var probe = CreateProbe(failureThreshold: 1);

        await CreateService().CheckAsync(probe);

        _incidents.DidNotReceive().Add(Arg.Any<Incident>());
        orphan.FailedChecks.Should().Be(2);
    }

    [Fact]
    public async Task ContinuedFailureUpdatesTheOpenIncidentSoTheCauseStaysCurrent()
    {
        var open = new Incident(1, Now.AddMinutes(-5), IncidentSeverity.Critical, "First reason.");
        _incidents.GetActiveForProbeAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(open);

        var probe = CreateProbe(failureThreshold: 1);
        GivenProbeReturns(ProbeExecutionResult.Failure(ProbeOutcome.Timeout, "First reason."));
        await CreateService().CheckAsync(probe);

        GivenProbeReturns(ProbeExecutionResult.Failure(ProbeOutcome.ConnectionRefused, "Connection refused."));
        await CreateService().CheckAsync(probe);

        open.Cause.Should().Be("Connection refused.");
    }

    [Fact]
    public async Task EveryCheckIsBroadcast_EvenWhenNothingChanged()
    {
        // Live latency readings are useful on their own, so the dashboard receives all of
        // them and not only state transitions.
        GivenProbeReturns(ProbeExecutionResult.Success(11));
        var probe = CreateProbe();

        await CreateService().CheckAsync(probe);
        await CreateService().CheckAsync(probe);

        await _notifier.Received(2).ProbeCheckedAsync(Arg.Any<ProbeCheckedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncidentNotificationsCarryTheDeviceAndTarget()
    {
        GivenProbeReturns(ProbeExecutionResult.Failure(ProbeOutcome.Timeout, "No reply."));
        _incidents.GetActiveForProbeAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((Incident?)null);
        var probe = CreateProbe(failureThreshold: 1);

        await CreateService().CheckAsync(probe);

        await _notifier.Received(1).IncidentOpenedAsync(
            Arg.Is<IncidentNotification>(n => n.DeviceName == "Edge Router" && n.Target == "10.0.0.1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoIncidentNotificationIsSentForAHealthyCheck()
    {
        GivenProbeReturns(ProbeExecutionResult.Success(9));
        var probe = CreateProbe();

        await CreateService().CheckAsync(probe);

        await _notifier.DidNotReceive().IncidentOpenedAsync(Arg.Any<IncidentNotification>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().IncidentResolvedAsync(Arg.Any<IncidentNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationHappensAfterTheCommit_SoClientsNeverSeeUnsavedState()
    {
        GivenProbeReturns(ProbeExecutionResult.Success(9));
        var probe = CreateProbe();

        var committed = false;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            committed = true;
            return 1;
        });

        var notifiedAfterCommit = false;
        await _notifier.ProbeCheckedAsync(Arg.Any<ProbeCheckedNotification>(), Arg.Any<CancellationToken>());
        _notifier.ClearReceivedCalls();
        _notifier
            .When(n => n.ProbeCheckedAsync(Arg.Any<ProbeCheckedNotification>(), Arg.Any<CancellationToken>()))
            .Do(_ => notifiedAfterCommit = committed);

        await CreateService().CheckAsync(probe);

        notifiedAfterCommit.Should().BeTrue();
    }

    [Fact]
    public async Task TheHttpStatusCodeIsPersistedWithTheResult()
    {
        GivenProbeReturns(new ProbeExecutionResult(ProbeOutcome.UnexpectedStatusCode, 120, 502, "Expected HTTP 200, got 502."));
        var probe = CreateProbe(failureThreshold: 1);

        await CreateService().CheckAsync(probe);

        _results.Received(1).Add(Arg.Is<ProbeResult>(r => r.StatusCode == 502 && !r.IsSuccess));
    }
}
