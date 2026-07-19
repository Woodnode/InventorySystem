using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Contrat d'accès à l'agrégat Product. Implémenté dans Infrastructure (DIP).
/// Un repository = un seul agrégat (voir SOLID §4 du plan).
/// </summary>
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Recherche exacte par SKU (normalisé via Sku.Create) — alimente le scan mobile.</summary>
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Liste complète, non paginée — réservée à l'export (CSV/Excel) qui a besoin
    /// de tout le catalogue, jamais d'une page.</summary>
    Task<IReadOnlyList<Product>> ListAsync(CancellationToken ct = default);

    /// <summary>Page triée par nom — utilisée par l'écran de liste produits (catalogue non
    /// borné dans le temps, contrairement à Warehouses/Suppliers qui restent petits).</summary>
    Task<(IReadOnlyList<Product> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize, CancellationToken ct = default);

    Task AddAsync(Product product, CancellationToken ct = default);
    void Update(Product product);
}
