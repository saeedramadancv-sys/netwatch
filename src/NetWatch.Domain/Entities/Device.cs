using NetWatch.Domain.Common;
using NetWatch.Domain.Enums;

namespace NetWatch.Domain.Entities;

/// <summary>
/// A monitored target: a router, a server, a public website — anything addressable
/// by IP or hostname. A device owns one or more <see cref="Probe"/>s; the device
/// itself never carries health, because a host can be pingable while the service
/// it hosts is dead.
/// </summary>
public class Device : BaseEntity
{
    private readonly List<Probe> _probes = [];

    // EF Core materialisation constructor.
    private Device()
    {
        Name = string.Empty;
        Hostname = string.Empty;
    }

    public Device(string name, string hostname, DeviceCategory category, string? site = null, string? description = null)
    {
        Name = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(name, nameof(name)), 100, nameof(name));
        Hostname = Guard.AgainstInvalidHost(hostname, nameof(hostname));
        Category = category;
        Site = site?.Trim();
        Description = description?.Trim();
        IsEnabled = true;
    }

    public string Name { get; private set; }

    /// <summary>IP address or DNS name the probes will target.</summary>
    public string Hostname { get; private set; }

    public DeviceCategory Category { get; private set; }

    /// <summary>Physical or logical grouping, e.g. "HQ - Floor 2" or "AWS eu-central-1".</summary>
    public string? Site { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// When false, the scheduler skips every probe on this device. Used for planned
    /// maintenance so a known outage does not generate incidents or dent uptime.
    /// </summary>
    public bool IsEnabled { get; private set; }

    public IReadOnlyCollection<Probe> Probes => _probes.AsReadOnly();

    public void Update(string name, string hostname, DeviceCategory category, string? site, string? description)
    {
        Name = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(name, nameof(name)), 100, nameof(name));
        Hostname = Guard.AgainstInvalidHost(hostname, nameof(hostname));
        Category = category;
        Site = site?.Trim();
        Description = description?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
        {
            return;
        }

        IsEnabled = enabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AddProbe(Probe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var duplicate = _probes.Any(p => p.Type == probe.Type && p.Port == probe.Port && p.HttpPath == probe.HttpPath);
        if (duplicate)
        {
            throw new DomainException("An identical probe is already configured on this device.");
        }

        _probes.Add(probe);

        // Sets the back-reference immediately rather than waiting for EF Core to fix up
        // navigations after a save, so a probe knows its host the moment it is attached.
        // Without this, anything reading probe.Device before a round trip - Describe(),
        // every executor - would see null.
        probe.AttachTo(this);

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RemoveProbe(Probe probe)
    {
        if (_probes.Remove(probe))
        {
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
