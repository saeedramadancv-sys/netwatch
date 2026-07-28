using NetWatch.Application.Probes;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Application.Devices;

public static class DeviceMapping
{
    /// <summary>
    /// Rolls several probe states into the single status a device tile shows.
    ///
    /// Worst-wins, with one deliberate ordering choice: a device with one healthy probe
    /// and one that has never run reports Up, not Unknown. Something is answering, and
    /// showing "unknown" would hide that.
    /// </summary>
    public static ProbeState Worst(IEnumerable<ProbeState> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        var worst = ProbeState.Unknown;

        foreach (var state in states)
        {
            switch (state)
            {
                case ProbeState.Down:
                    return ProbeState.Down;

                case ProbeState.Degraded:
                    worst = ProbeState.Degraded;
                    break;

                case ProbeState.Up when worst != ProbeState.Degraded:
                    worst = ProbeState.Up;
                    break;
            }
        }

        return worst;
    }

    public static DeviceResponse ToResponse(this Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceResponse(
            device.Id,
            device.Name,
            device.Hostname,
            device.Category,
            device.Site,
            device.Description,
            device.IsEnabled,
            device.Probes.Count,
            Worst(device.Probes.Select(p => p.State)),
            device.CreatedAtUtc);
    }

    public static DeviceDetailResponse ToDetailResponse(this Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceDetailResponse(
            device.Id,
            device.Name,
            device.Hostname,
            device.Category,
            device.Site,
            device.Description,
            device.IsEnabled,
            Worst(device.Probes.Select(p => p.State)),
            device.CreatedAtUtc,
            device.UpdatedAtUtc,
            [.. device.Probes.Select(p => p.ToResponse())]);
    }
}
