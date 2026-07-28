namespace NetWatch.Infrastructure.Persistence;

public enum DatabaseProvider
{
    /// <summary>File-based, zero install. Used for local development and CI.</summary>
    Sqlite = 0,

    /// <summary>Production target.</summary>
    SqlServer = 1
}

/// <summary>
/// Which relational engine to run against, bound from the <c>Database</c> configuration
/// section.
///
/// The application code never learns the answer: it depends on EF Core's provider-neutral
/// API only, with no raw SQL and no provider-specific column types. Switching engines is a
/// configuration change plus the matching migration assembly.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>
    /// Applies pending migrations on startup. Convenient for containers and demos;
    /// production deployments normally run migrations as a separate, reviewable step.
    /// </summary>
    public bool MigrateOnStartup { get; set; } = true;

    /// <summary>Creates roles, an initial administrator and sample devices on first run.</summary>
    public bool SeedOnStartup { get; set; } = true;

    /// <summary>
    /// Migration assemblies are per provider: the SQL each migration emits is generated
    /// for one engine, so a single shared set cannot serve both.
    /// </summary>
    public string MigrationsAssembly => Provider switch
    {
        DatabaseProvider.SqlServer => "NetWatch.Migrations.SqlServer",
        _ => "NetWatch.Migrations.Sqlite"
    };
}
