namespace NetWatch.Domain.Enums;

/// <summary>
/// Classification of a monitored target, used for grouping and dashboard filtering.
/// </summary>
public enum DeviceCategory
{
    Router = 0,
    Switch = 1,
    Firewall = 2,
    AccessPoint = 3,
    Server = 4,
    Printer = 5,
    Website = 6,
    Other = 99
}
