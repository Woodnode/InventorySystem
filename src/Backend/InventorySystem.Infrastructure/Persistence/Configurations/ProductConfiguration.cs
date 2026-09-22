using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySystem.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        // Value Object Sku converti vers/depuis une simple colonne texte.
        builder.Property(p => p.Sku)
            .HasConversion(sku => sku.Value, value => Sku.Create(value))
            .HasColumnName("sku")
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(p => p.Sku).IsUnique();

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.LowStockThreshold).IsRequired();

        // Nouveaux champs d'édition
        builder.Property(p => p.ProjectCode).HasMaxLength(200);
        builder.Property(p => p.Collection).HasMaxLength(150);
        builder.Property(p => p.VolumeNumber).HasMaxLength(50);
        builder.Property(p => p.ProductType).HasMaxLength(100);
        builder.Property(p => p.Company).HasMaxLength(150);

        builder.HasOne<Supplier>().WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull);
    }
}
