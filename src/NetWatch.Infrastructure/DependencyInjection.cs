using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Monitoring;
using NetWatch.Infrastructure.Identity;
using NetWatch.Infrastructure.Monitoring;
using NetWatch.Infrastructure.Persistence;
using NetWatch.Infrastructure.Persistence.Repositories;
using NetWatch.Infrastructure.Probing;

namespace NetWatch.Infrastructure;

/// <summary>
/// Single composition point for everything Infrastructure owns. The API layer calls
/// <c>AddInfrastructure</c> and knows nothing about EF Core, Identity, or sockets.
///
/// Nothing here reads a configuration <i>value</i> during registration. Binding is
/// deferred to the service provider, because configuration sources can still be added
/// after this method runs — WebApplicationFactory does exactly that to point integration
/// tests at an in-memory database. Reading eagerly would silently capture the defaults
/// and ignore whatever the host layered on afterwards.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<MonitoringOptions>(configuration.GetSection(MonitoringOptions.SectionName));

        // ValidateOnStart turns a missing or too-short signing key into a startup failure
        // instead of a 500 on the first login attempt in production.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        AddPersistence(services);
        AddIdentity(services);
        AddProbing(services);

        // TimeProvider instead of a hand-rolled IDateTimeProvider: it is the framework
        // abstraction since .NET 8, and tests can substitute FakeTimeProvider to drive
        // time-dependent logic without waiting on a real clock.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IProbeCheckService, ProbeCheckService>();

        // Registered unconditionally; each service exits immediately when monitoring is
        // disabled. Deciding here would require reading configuration eagerly.
        services.AddHostedService<MonitoringSchedulerService>();
        services.AddHostedService<ResultRetentionService>();

        return services;
    }

    private static void AddPersistence(IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, builder) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=netwatch.db";

            switch (options.Provider)
            {
                case DatabaseProvider.SqlServer:
                    builder.UseSqlServer(connectionString, sql =>
                    {
                        sql.MigrationsAssembly(options.MigrationsAssembly);

                        // Monitoring keeps writing through brief database blips; retrying
                        // transient faults is cheaper than losing a sweep.
                        sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                    });
                    break;

                default:
                    builder.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(options.MigrationsAssembly));
                    break;
            }
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IProbeRepository, ProbeRepository>();
        services.AddScoped<IProbeResultRepository, ProbeResultRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<DbInitializer>();
    }

    private static void AddIdentity(IServiceCollection services)
    {
        // AddIdentityCore rather than AddIdentity: this is a token API with no cookie
        // authentication, and AddIdentity would register a cookie scheme that silently
        // competes with JWT bearer for the default challenge.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;

                // Throttles online password guessing without needing a separate rate limiter.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<TokenService>();
        services.AddScoped<IAuthService, AuthService>();
    }

    private static void AddProbing(IServiceCollection services)
    {
        services.AddHttpClient(HttpProbeExecutor.HttpClientName, client =>
            {
                // Timeouts are enforced per probe with a CancellationToken, so the client's
                // own timeout must not fire first and mask the configured value.
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("NetWatch/1.0 (+monitoring)");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // A monitored endpoint returning 301 is a finding, not something to follow:
                // the probe asserts the status the operator configured.
                AllowAutoRedirect = false,
                UseCookies = false
            });

        // Executors are stateless and thread safe, so one instance each is enough.
        services.AddSingleton<IProbeExecutor, IcmpProbeExecutor>();
        services.AddSingleton<IProbeExecutor, TcpProbeExecutor>();
        services.AddSingleton<IProbeExecutor, HttpProbeExecutor>();
        services.AddSingleton<IProbeRunner, ProbeRunner>();
    }
}
