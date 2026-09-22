using InventorySystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySystem.Infrastructure.Persistence.Configurations;

public sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stocks");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ProductId).IsRequired();
        builder.Property(s => s.WarehouseId).IsRequired();
        builder.Property(s => s.Quantity).IsRequired();

        // Champs de logistique
        builder.Property(s => s.Section).HasMaxLength(50);
        builder.Property(s => s.Space).HasMaxLength(50);
        builder.Property(s => s.Pallet).HasMaxLength(50);
        builder.Property(s => s.DistributorName).HasMaxLength(150);
        builder.Property(s => s.Comment).HasMaxLength(1000);
        builder.Property(s => s.ResponsibleName).HasMaxLength(150);

        // Une seule ligne de stock par (produit, entrepôt) — c'est la clé métier réelle.
        builder.HasIndex(s => new { s.ProductId, s.WarehouseId }).IsUnique();

        // Deux mouvements concurrents sur le même (produit, entrepôt) ne doivent jamais
        // s'écraser silencieusement : Version est mappée sur xmin, la colonne système
        // Postgres mise à jour automatiquement à chaque écriture de la ligne.
        builder.Property(s => s.Version).IsRowVersion();

        builder.HasOne<Product>().WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}
