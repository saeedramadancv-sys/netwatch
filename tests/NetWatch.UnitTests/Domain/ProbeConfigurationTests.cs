using FluentAssertions;
using NetWatch.Domain.Common;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.UnitTests.Domain;

/// <summary>
/// Construction rules, latency grading, and due-time calculation.
/// </summary>
public class ProbeConfigurationTests
{
    private static readonly DateTime At = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TcpProbe_RequiresAPort()
    {
        var act = () => new Probe(ProbeType.Tcp, port: null);

        act.Should().Throw<DomainException>().WithMessage("*requires a port*");
    }

    [Fact]
    public void IcmpProbe_RejectsAPort()
    {
        var act = () => new Probe(ProbeType.Icmp, port: 443);

        act.Should().Throw<DomainException>().WithMessage("*cannot target a port*");
    }

    [Fact]
    public void HttpProbe_RejectsAPathThatIsNotRooted()
    {
        var act = () => new Probe(ProbeType.Http, httpPath: "health");

        act.Should().Throw<DomainException>().WithMessage("*must start with*");
    }

    [Fact]
    public void TimeoutLongerThanTheInterval_IsRejectedBecauseChecksWouldOverlap()
    {
        var act = () => new Probe(ProbeType.Icmp, intervalSeconds: 5, timeoutMs: 10_000);

        act.Should().Throw<DomainException>().WithMessage("*shorter than the check interval*");
    }

    [Fact]
    public void DegradedThresholdAboveTheTimeout_IsRejectedBecauseItCouldNeverTrigger()
    {
        var act = () => new Probe(ProbeType.Icmp, timeoutMs: 2_000, degradedLatencyMs: 5_000);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Grade_MarksASlowSuccessAsDegraded()
    {
        var probe = new Probe(ProbeType.Icmp, timeoutMs: 2_000, degradedLatencyMs: 100);

        probe.Grade(ProbeOutcome.Success, 250).Should().Be(ProbeOutcome.Degraded);
    }

    [Fact]
    public void Grade_LeavesAFastSuccessAlone()
    {
        var probe = new Probe(ProbeType.Icmp, timeoutMs: 2_000, degradedLatencyMs: 100);

        probe.Grade(ProbeOutcome.Success, 50).Should().Be(ProbeOutcome.Success);
    }

    [Fact]
    public void Grade_WithoutAThreshold_NeverReportsDegraded()
    {
        var probe = new Probe(ProbeType.Icmp, timeoutMs: 2_000, degradedLatencyMs: null);

        probe.Grade(ProbeOutcome.Success, 1_999).Should().Be(ProbeOutcome.Success);
    }

    [Fact]
    public void Grade_NeverUpgradesAFailureToDegraded()
    {
        var probe = new Probe(ProbeType.Icmp, timeoutMs: 2_000, degradedLatencyMs: 10);

        probe.Grade(ProbeOutcome.Timeout, null).Should().Be(ProbeOutcome.Timeout);
    }

    [Fact]
    public void AProbeThatHasNeverRun_IsDue()
    {
        var probe = new Probe(ProbeType.Icmp, intervalSeconds: 60);

        probe.IsDue(At).Should().BeTrue();
    }

    [Fact]
    public void AProbeCheckedWithinItsInterval_IsNotDue()
    {
        var probe = new Probe(ProbeType.Icmp, intervalSeconds: 60);
        probe.RecordResult(ProbeOutcome.Success, 10, At);

        probe.IsDue(At.AddSeconds(59)).Should().BeFalse();
    }

    [Fact]
    public void AProbeIsDueTheInstantItsIntervalElapses()
    {
        var probe = new Probe(ProbeType.Icmp, intervalSeconds: 60);
        probe.RecordResult(ProbeOutcome.Success, 10, At);

        probe.IsDue(At.AddSeconds(60)).Should().BeTrue();
    }

    [Fact]
    public void ADisabledProbe_IsNeverDue()
    {
        var probe = new Probe(ProbeType.Icmp, intervalSeconds: 60);
        probe.SetEnabled(false);

        probe.IsDue(At.AddHours(1)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, null, "https://example.com/health")]
    [InlineData(false, null, "http://example.com/health")]
    [InlineData(true, 443, "https://example.com/health")]
    [InlineData(true, 8443, "https://example.com:8443/health")]
    public void BuildHttpUrl_OmitsTheDefaultPortForTheScheme(bool useHttps, int? port, string expected)
    {
        var probe = new Probe(ProbeType.Http, httpPath: "/health", useHttps: useHttps, port: port);

        probe.BuildHttpUrl("example.com").Should().Be(expected);
    }

    [Fact]
    public void Describe_RendersTheTargetForEachProbeType()
    {
        var device = new Device("Edge", "10.0.0.1", DeviceCategory.Router);

        var icmp = new Probe(ProbeType.Icmp);
        var tcp = new Probe(ProbeType.Tcp, port: 22);
        device.AddProbe(icmp);
        device.AddProbe(tcp);

        icmp.Describe().Should().Be("10.0.0.1");
        tcp.Describe().Should().Be("10.0.0.1:22");
    }

    [Fact]
    public void AddingAnIdenticalProbeTwice_IsRejected()
    {
        var device = new Device("Edge", "10.0.0.1", DeviceCategory.Router);
        device.AddProbe(new Probe(ProbeType.Tcp, port: 22));

        var act = () => device.AddProbe(new Probe(ProbeType.Tcp, port: 22));

        act.Should().Throw<DomainException>().WithMessage("*already configured*");
    }

    [Fact]
    public void AddingAProbe_LinksItToItsDeviceImmediately()
    {
        var device = new Device("Edge", "10.0.0.1", DeviceCategory.Router);
        var probe = new Probe(ProbeType.Icmp);

        device.AddProbe(probe);

        probe.Device.Should().BeSameAs(device);
    }
}
