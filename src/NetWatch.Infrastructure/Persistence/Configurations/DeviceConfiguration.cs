using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetWatch.Domain.Entities;

namespace NetWatch.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.Hostname)
            .IsRequired()
            .HasMaxLength(253);

        builder.Property(d => d.Site)
            .HasMaxLength(100);

        builder.Property(d => d.Description)
            .HasMaxLength(500);

        // Stored as int rather than a string so adding or renaming a category never
        // requires a data migration.
        builder.Property(d => d.Category)
            .HasConversion<int>();

        // One device per host: two entries for the same address would double-count it
        // on the dashboard and split its history.
        builder.HasIndex(d => d.Hostname).IsUnique();
        builder.HasIndex(d => d.Site);

        builder.HasMany(d => d.Probes)
            .WithOne(p => p.Device)
            .HasForeignKey(p => p.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // The collection is exposed as a read-only view over a private list, so EF must
        // read and write the backing field instead of the property.
        builder.Metadata
            .FindNavigation(nameof(Device.Probes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
