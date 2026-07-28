using FluentAssertions;
using NetWatch.Application.Devices;
using NetWatch.Domain.Common;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.UnitTests.Domain;

public class DeviceTests
{
    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.254")]
    [InlineData("2001:db8::1")]
    [InlineData("example.com")]
    [InlineData("sub.domain.example.co.uk")]
    [InlineData("switch-01")]
    public void ValidHostsAreAccepted(string hostname)
    {
        var act = () => new Device("Target", hostname, DeviceCategory.Server);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has spaces")]
    [InlineData("-leadinghyphen.com")]
    [InlineData("double..dot.com")]
    [InlineData("under_score.com")]
    public void InvalidHostsAreRejected(string hostname)
    {
        var act = () => new Device("Target", hostname, DeviceCategory.Server);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void HostnamesAreTrimmed()
    {
        var device = new Device("Target", "  10.0.0.1  ", DeviceCategory.Server);

        device.Hostname.Should().Be("10.0.0.1");
    }

    [Fact]
    public void ANewDeviceIsEnabled()
    {
        new Device("Target", "10.0.0.1", DeviceCategory.Server).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void SettingTheSameEnabledValue_DoesNotTouchTheAuditStamp()
    {
        var device = new Device("Target", "10.0.0.1", DeviceCategory.Server);

        device.SetEnabled(true);

        device.UpdatedAtUtc.Should().BeNull();
    }
}

/// <summary>
/// Rolling several probe states into the single status shown on a device tile.
/// </summary>
public class DeviceStatusRollupTests
{
    [Fact]
    public void NoProbes_IsUnknown()
    {
        DeviceMapping.Worst([]).Should().Be(ProbeState.Unknown);
    }

    [Fact]
    public void AnyDownProbe_MakesTheDeviceDown()
    {
        DeviceMapping.Worst([ProbeState.Up, ProbeState.Degraded, ProbeState.Down]).Should().Be(ProbeState.Down);
    }

    [Fact]
    public void DegradedBeatsUp()
    {
        DeviceMapping.Worst([ProbeState.Up, ProbeState.Degraded]).Should().Be(ProbeState.Degraded);
    }

    [Fact]
    public void UpBeatsUnknown_SoAnAnsweringDeviceIsNotHiddenBehindANeverRunProbe()
    {
        DeviceMapping.Worst([ProbeState.Unknown, ProbeState.Up]).Should().Be(ProbeState.Up);
    }

    [Fact]
    public void AllUnknown_StaysUnknown()
    {
        DeviceMapping.Worst([ProbeState.Unknown, ProbeState.Unknown]).Should().Be(ProbeState.Unknown);
    }
}
