namespace NetWatch.Infrastructure.Caching;

/// <summary>
/// Distributed cache settings, bound from the <c>Cache</c> configuration section.
///
/// Redis is opt-in by connection string. With none configured the application registers
/// an in-process distributed cache instead, so a fresh clone, the test host and a
/// single-instance deployment all run with no broker to install — while the code path
/// under test stays identical to the one production takes.
/// </summary>
public class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Turns read-through caching off entirely, leaving the services undecorated.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Backstop lifetime for a cached dashboard summary.
    ///
    /// Kept at roughly one monitoring tick: state changes retire the entry immediately,
    /// so this only bounds how long a summary can drift when an invalidation is lost —
    /// for instance if the cache restarted between the write and the eviction.
    /// </summary>
    public int DashboardSeconds { get; set; } = 10;

    /// <summary>
    /// Key prefix, so several environments can share one Redis instance without one
    /// environment's dashboard being served to another.
    /// </summary>
    public string InstanceName { get; set; } = "netwatch:";
}
