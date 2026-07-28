using NetWatch.Application.Probes;
using NetWatch.Domain.Enums;

namespace NetWatch.Application.Devices;

/// <summary>
/// Summary row for device lists.
/// </summary>
/// <param name="Status">Worst state across the device's probes — what a dashboard tile shows.</param>
public sealed record DeviceResponse(
    int Id,
    string Name,
    string Hostname,
    DeviceCategory Category,
    string? Site,
    string? Description,
    bool IsEnabled,
    int ProbeCount,
    ProbeState Status,
    DateTime CreatedAtUtc);

/// <summary>Device plus its probes, for the detail page.</summary>
public sealed record DeviceDetailResponse(
    int Id,
    string Name,
    string Hostname,
    DeviceCategory Category,
    string? Site,
    string? Description,
    bool IsEnabled,
    ProbeState Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<ProbeResponse> Probes);

public sealed record CreateDeviceRequest(
    string Name,
    string Hostname,
    DeviceCategory Category,
    string? Site,
    string? Description);

public sealed record UpdateDeviceRequest(
    string Name,
    string Hostname,
    DeviceCategory Category,
    string? Site,
    string? Description);
