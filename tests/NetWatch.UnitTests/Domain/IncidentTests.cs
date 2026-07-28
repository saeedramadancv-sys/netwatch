using FluentAssertions;
using NetWatch.Domain.Common;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.UnitTests.Domain;

public class IncidentTests
{
    private static readonly DateTime Started = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Incident Open() => new(probeId: 1, Started, IncidentSeverity.Critical, "Timeout after 5000ms.");

    [Fact]
    public void ANewIncidentIsOpenAndCountsItsFirstFailure()
    {
        var incident = Open();

        incident.Status.Should().Be(IncidentStatus.Open);
        incident.IsActive.Should().BeTrue();
        incident.FailedChecks.Should().Be(1);
        incident.ResolvedAtUtc.Should().BeNull();
    }

    [Fact]
    public void RecordingFailures_AccumulatesTheCountAndKeepsTheLatestCause()
    {
        var incident = Open();

        incident.RecordFailure("Connection refused.");
        incident.RecordFailure("Still refused.");

        incident.FailedChecks.Should().Be(3);
        incident.Cause.Should().Be("Still refused.");
    }

    [Fact]
    public void Resolving_ClosesTheIncidentAndFixesItsDuration()
    {
        var incident = Open();

        incident.Resolve(Started.AddMinutes(7));

        incident.Status.Should().Be(IncidentStatus.Resolved);
        incident.IsActive.Should().BeFalse();
        incident.Duration.Should().Be(TimeSpan.FromMinutes(7));
    }

    [Fact]
    public void ResolvingTwice_IsANoOpRatherThanAnError()
    {
        var incident = Open();
        incident.Resolve(Started.AddMinutes(5));

        var act = () => incident.Resolve(Started.AddMinutes(9));

        act.Should().NotThrow();
        incident.Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ResolvingBeforeItStarted_IsRejected()
    {
        var incident = Open();

        var act = () => incident.Resolve(Started.AddMinutes(-1));

        act.Should().Throw<DomainException>().WithMessage("*before it started*");
    }

    [Fact]
    public void AcknowledgingRecordsWhoTookIt()
    {
        var incident = Open();

        incident.Acknowledge("user-42", Started.AddMinutes(2));

        incident.Status.Should().Be(IncidentStatus.Acknowledged);
        incident.AcknowledgedByUserId.Should().Be("user-42");
        incident.AcknowledgedAtUtc.Should().Be(Started.AddMinutes(2));
    }

    [Fact]
    public void AnAcknowledgedIncidentIsStillActive_BecauseTheOutageIsNotOver()
    {
        var incident = Open();

        incident.Acknowledge("user-42", Started.AddMinutes(2));

        incident.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AcknowledgingAResolvedIncident_IsRejected()
    {
        var incident = Open();
        incident.Resolve(Started.AddMinutes(1));

        var act = () => incident.Acknowledge("user-42", Started.AddMinutes(2));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Escalating_RaisesSeverity()
    {
        var incident = new Incident(1, Started, IncidentSeverity.Warning, "Slow.");

        incident.Escalate(IncidentSeverity.Critical, "Now unreachable.");

        incident.Severity.Should().Be(IncidentSeverity.Critical);
        incident.Cause.Should().Be("Now unreachable.");
    }

    [Fact]
    public void SeverityNeverDropsWhileAnIncidentIsOpen()
    {
        // A critical outage that briefly looks merely slow was still a critical outage.
        var incident = Open();

        incident.Escalate(IncidentSeverity.Warning, "Responding, but slowly.");

        incident.Severity.Should().Be(IncidentSeverity.Critical);
    }

    [Fact]
    public void AnUnresolvedIncidentReportsItsRunningDuration()
    {
        var incident = Open();

        incident.DurationAt(Started.AddMinutes(3)).Should().Be(TimeSpan.FromMinutes(3));
        incident.Duration.Should().BeNull();
    }
}
