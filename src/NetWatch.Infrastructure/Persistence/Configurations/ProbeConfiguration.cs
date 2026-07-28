using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetWatch.Domain.Entities;

namespace NetWatch.Infrastructure.Persistence.Configurations;

public class ProbeConfiguration : IEntityTypeConfiguration<Probe>
{
    public void Configure(EntityTypeBuilder<Probe> builder)
    {
        builder.ToTable("Probes");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Type).HasConversion<int>();
        builder.Property(p => p.State).HasConversion<int>();
        builder.Property(p => p.LastOutcome).HasConversion<int?>();

        builder.Property(p => p.HttpPath).HasMaxLength(500);

        builder.HasIndex(p => p.DeviceId);

        // The scheduler's hot query is "which enabled probes are due?". Ordering the
        // index by IsEnabled first lets disabled probes be skipped without touching them.
        builder.HasIndex(p => new { p.IsEnabled, p.LastCheckedAtUtc });

        builder.HasMany<ProbeResult>()
            .WithOne(r => r.Probe)
            .HasForeignKey(r => r.ProbeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<Incident>()
            .WithOne(i => i.Probe)
            .HasForeignKey(i => i.ProbeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
