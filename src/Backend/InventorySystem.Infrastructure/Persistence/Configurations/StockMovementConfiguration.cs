using InventorySystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySystem.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ProductId).IsRequired();
        builder.Property(m => m.WarehouseId).IsRequired();
        builder.Property(m => m.ToWarehouseId);

        builder.Property(m => m.Type)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(m => m.Quantity).IsRequired();
        builder.Property(m => m.Reason).HasMaxLength(500);

        // Le ClientGuid garantit l'idempotence : deux synchronisations du même
        // mouvement mobile ne créent jamais deux lignes (plan §8.3).
        builder.HasIndex(m => m.ClientGuid).IsUnique();

        // Toutes les lectures de l'historique filtrent par ProductId et trient par
        // CreatedAtUtc desc (ListByProductAsync/ListByProductPagedAsync) — sans cet index
        // composite, la requête devient un sequential scan quand la table grossit.
        builder.HasIndex(m => new { m.ProductId, m.CreatedAtUtc });

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(m => m.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Pas de contrainte FK sur ToWarehouseId : EF Core ne permet pas deux FK
        // "Restrict" vers la même table de façon fiable multi-provider ; la validation
        // d'existence est déjà faite dans RecordMovementCommandValidator (Application).
    }
}
