using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;
using NetWatch.Infrastructure.Identity;

namespace NetWatch.Infrastructure.Persistence;

/// <summary>
/// Brings an empty database up to a usable state: schema, roles, a first administrator
/// and a handful of demo targets.
///
/// Seeding is written to be idempotent — every step checks before it writes — so it can
/// run on every start without duplicating data or failing a container restart.
/// </summary>
public class DbInitializer(
    AppDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration,
    ILogger<DbInitializer> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("Database schema is up to date.");
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            // string.Join allocates whether or not anything is listening.
            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Length, string.Join(", ", pending));
        }
        await context.Database.MigrateAsync(cancellationToken);
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedAdministratorAsync();
        await SeedSampleDevicesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not create role '{role}': {Describe(result)}");
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Created role {Role}.", role);
            }
        }
    }

    private async Task SeedAdministratorAsync()
    {
        var email = configuration["Seed:AdminEmail"] ?? "admin@netwatch.local";
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(password))
        {
            // Refusing to invent a default password is intentional. A well-known
            // credential baked into a public repository is the single most common way a
            // demo deployment gets taken over.
            logger.LogWarning(
                "No Seed:AdminPassword configured, so no administrator was created. Set it via user-secrets or an environment variable.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = configuration["Seed:AdminFullName"] ?? "NetWatch Administrator",
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException($"Could not create the administrator account: {Describe(created)}");
        }

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Created administrator {Email}.", email);
        }
    }

    private async Task SeedSampleDevicesAsync(CancellationToken cancellationToken)
    {
        if (await context.Devices.AnyAsync(cancellationToken))
        {
            return;
        }

        // Public, well-known endpoints so a fresh clone shows live data immediately,
        // even for someone with no lab network to point it at.
        var dns = new Device("Cloudflare DNS", "1.1.1.1", DeviceCategory.Server, "Public", "Public resolver used as a reachability baseline.");
        dns.AddProbe(new Probe(ProbeType.Icmp, intervalSeconds: 60, timeoutMs: 3_000, degradedLatencyMs: 150));
        dns.AddProbe(new Probe(ProbeType.Tcp, intervalSeconds: 60, timeoutMs: 3_000, port: 53));

        var web = new Device("Example Site", "example.com", DeviceCategory.Website, "Public", "HTTP availability check.");
        web.AddProbe(new Probe(ProbeType.Http, intervalSeconds: 60, timeoutMs: 5_000, httpPath: "/", degradedLatencyMs: 1_000));

        var gateway = new Device("Default Gateway", "192.168.1.1", DeviceCategory.Router, "HQ", "Edit this to match your own network.");
        gateway.AddProbe(new Probe(ProbeType.Icmp, intervalSeconds: 30, timeoutMs: 2_000, degradedLatencyMs: 50));

        context.Devices.AddRange(dns, web, gateway);
        await context.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Seeded {Count} sample devices.", 3);
        }
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));
}
