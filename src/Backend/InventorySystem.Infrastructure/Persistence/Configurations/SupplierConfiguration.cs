using InventorySystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySystem.Infrastructure.Persistence.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ContactEmail).HasMaxLength(256);
        builder.Property(s => s.Phone).HasMaxLength(30);
        // HasDefaultValue(true) : les fournisseurs existants ne doivent pas être désactivés
        // silencieusement par cette migration (le défaut EF Core sans configuration explicite
        // est `false`, la valeur CLR par défaut de bool — piège identifié au ré-audit).
        builder.Property(s => s.IsActive).IsRequired().HasDefaultValue(true);
    }
}
