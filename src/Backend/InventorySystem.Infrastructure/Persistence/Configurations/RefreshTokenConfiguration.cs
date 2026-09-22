using InventorySystem.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySystem.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.FamilyId).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => new { t.FamilyId, t.RevokedAtUtc });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
