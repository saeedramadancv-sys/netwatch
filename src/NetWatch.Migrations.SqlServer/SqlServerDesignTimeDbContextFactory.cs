using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NetWatch.Infrastructure.Persistence;

namespace NetWatch.Migrations.SqlServer;

/// <summary>
/// Design-time counterpart for SQL Server. Kept in its own assembly because migrations
/// carry provider-specific SQL: the SQLite and SQL Server sets describe the same model
/// but cannot be shared.
/// </summary>
public class SqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=NetWatch;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly(typeof(SqlServerDesignTimeDbContextFactory).Assembly.GetName().Name))
            .Options;

        return new AppDbContext(options);
    }
}
