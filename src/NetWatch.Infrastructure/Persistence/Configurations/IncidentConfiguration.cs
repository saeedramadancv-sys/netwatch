using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetWatch.Domain.Entities;

namespace NetWatch.Infrastructure.Persistence.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incidents");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Status).HasConversion<int>();
        builder.Property(i => i.Severity).HasConversion<int>();

        builder.Property(i => i.Cause)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.AcknowledgedByUserId).HasMaxLength(450);

        // "Is this probe already broken?" runs after every failing check, so it needs to
        // be an index seek rather than a scan of incident history.
        builder.HasIndex(i => new { i.ProbeId, i.Status });

        // The incidents page is ordered newest first.
        builder.HasIndex(i => i.StartedAtUtc).IsDescending();
    }
}
