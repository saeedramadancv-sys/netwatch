namespace NetWatch.Infrastructure.Identity;

/// <summary>
/// The three roles the API authorises against. Constants rather than magic strings so a
/// typo in an <c>[Authorize(Roles = ...)]</c> attribute is a compile error instead of a
/// silently unreachable endpoint.
/// </summary>
public static class AppRoles
{
    /// <summary>Full control, including user management and deleting devices.</summary>
    public const string Admin = "Admin";

    /// <summary>Can add and edit devices and probes, and acknowledge incidents.</summary>
    public const string Operator = "Operator";

    /// <summary>Read-only access to dashboards, history and incidents.</summary>
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Operator, Viewer];
}
