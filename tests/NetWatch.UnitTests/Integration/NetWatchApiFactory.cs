using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace NetWatch.UnitTests.Integration;

/// <summary>
/// Boots the real API in memory for end-to-end request tests.
///
/// Three deliberate choices:
///
/// <para><b>A real database, not a fake one.</b> The tests run against SQLite in shared
/// in-memory mode, which exercises the actual migrations, indexes and relational
/// constraints. EF Core's in-memory provider would silently accept a schema the real one
/// rejects.</para>
///
/// <para><b>The connection is held open.</b> A SQLite in-memory database lives only while
/// at least one connection to it is open, so the factory keeps one for its lifetime.
/// Each factory instance gets a unique name, so parallel test classes cannot see each
/// other's data.</para>
///
/// <para><b>The scheduler is off.</b> Tests must not send real packets, and background
/// probing would make assertions about check counts non-deterministic.</para>
/// </summary>
public class NetWatchApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@netwatch.test";
    public const string AdminPassword = "IntegrationTest#1";

    private readonly string _databaseName = $"netwatch-tests-{Guid.NewGuid():N}";
    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = $"Data Source={_databaseName};Mode=Memory;Cache=Shared";

        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Database:Provider"] = "Sqlite",
                ["Database:MigrateOnStartup"] = "true",
                ["Database:SeedOnStartup"] = "true",

                ["Monitoring:Enabled"] = "false",

                // Fixed so tokens stay valid across requests within a test.
                ["Jwt:SigningKey"] = "integration-tests-signing-key-not-used-anywhere-else-0123456789",
                ["Jwt:Issuer"] = "NetWatch.Tests",
                ["Jwt:Audience"] = "NetWatch.Tests",

                ["Seed:AdminEmail"] = AdminEmail,
                ["Seed:AdminPassword"] = AdminPassword
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _keepAlive?.Dispose();
            _keepAlive = null;
        }
    }
}
