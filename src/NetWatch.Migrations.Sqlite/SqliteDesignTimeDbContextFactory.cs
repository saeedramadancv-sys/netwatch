using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NetWatch.Infrastructure.Persistence;

namespace NetWatch.Migrations.Sqlite;

/// <summary>
/// Lets <c>dotnet ef</c> build an <see cref="AppDbContext"/> without booting the API.
///
/// The connection string here is only used to pick the provider's SQL dialect while
/// scaffolding a migration — no database is contacted, and the running application always
/// supplies its own connection string from configuration.
/// </summary>
public class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(
                "Data Source=design-time.db",
                sqlite => sqlite.MigrationsAssembly(typeof(SqliteDesignTimeDbContextFactory).Assembly.GetName().Name))
            .Options;

        return new AppDbContext(options);
    }
}
