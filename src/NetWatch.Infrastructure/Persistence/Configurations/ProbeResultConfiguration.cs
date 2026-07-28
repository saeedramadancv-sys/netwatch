using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetWatch.Domain.Entities;

namespace NetWatch.Infrastructure.Persistence.Configurations;

public class ProbeResultConfiguration : IEntityTypeConfiguration<ProbeResult>
{
    public void Configure(EntityTypeBuilder<ProbeResult> builder)
    {
        builder.ToTable("ProbeResults");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Outcome).HasConversion<int>();
        builder.Property(r => r.ErrorMessage).HasMaxLength(500);

        // Every read of this table is "results for probe X between two timestamps",
        // so leading with ProbeId then CheckedAtUtc turns it into a single range seek.
        // Deliberately no covering INCLUDE clause here: that is a SQL Server-only
        // construct, and the model has to build identically on every supported provider.
        builder.HasIndex(r => new { r.ProbeId, r.CheckedAtUtc });

        // Retention deletes scan by age across all probes, which the composite index
        // above cannot serve.
        builder.HasIndex(r => r.CheckedAtUtc);
    }
}
