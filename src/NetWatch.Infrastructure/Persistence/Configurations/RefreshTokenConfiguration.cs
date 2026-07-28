using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetWatch.Infrastructure.Identity;

namespace NetWatch.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
        builder.Property(t => t.CreatedByIp).HasMaxLength(45);
        builder.Property(t => t.UserId).IsRequired().HasMaxLength(450);

        // Presented tokens are looked up by hash on every refresh; unique because two
        // tokens hashing to the same value would mean a collision worth failing loudly on.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
