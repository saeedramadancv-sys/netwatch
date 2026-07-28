using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Domain.Common;
using NetWatch.Domain.Entities;
using NetWatch.Infrastructure.Identity;

namespace NetWatch.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context: application tables plus the ASP.NET Core Identity schema.
///
/// Kept as one context (and therefore one connection and one transaction) so that saving
/// a check result, the probe's new state and an incident row is atomic. Splitting Identity
/// into its own context would buy separation on paper and cost transactional integrity.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IUnitOfWork
{
    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Probe> Probes => Set<Probe>();

    public DbSet<ProbeResult> ProbeResults => Set<ProbeResult>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Picks up every IEntityTypeConfiguration in this assembly, so adding a new
        // entity means adding one configuration file and nothing else.
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }

    /// <summary>
    /// Fills audit timestamps centrally so no caller can forget them, and so the
    /// entities themselves stay free of clock access.
    /// </summary>
    private void StampAuditFields()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    // Guards against a detached-then-attached entity resetting its creation time.
                    entry.Property(e => e.CreatedAtUtc).IsModified = false;
                    break;
            }
        }
    }
}
