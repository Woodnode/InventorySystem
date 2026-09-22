using InventorySystem.Application.Products.Dtos;
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

    /// <summary>
    /// Liste catalogue bornée (plafond <paramref name="maxRows"/>) — réservée à l'export.
    /// </summary>
    Task<IReadOnlyList<Product>> ListAsync(int maxRows, CancellationToken ct = default);

    /// <summary>Charge un sous-ensemble de produits par identifiants (résolution de noms à l'export).</summary>
    Task<IReadOnlyList<Product>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Produits en stock bas — page SQL (agrégation + filtre), plafonnée côté API.
    /// </summary>
    Task<(IReadOnlyList<(Product Product, int TotalQuantity)> Items, int TotalCount)> ListLowStockPagedAsync(
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Page triée — utilisée par l'écran de liste produits (catalogue non borné dans le temps,
    /// contrairement à Warehouses/Suppliers qui restent petits). <paramref name="search"/>
    /// filtre sur SKU ou nom (sous-chaîne, insensible à la casse) ; <paramref name="minQuantity"/>/
    /// <paramref name="maxQuantity"/>/<paramref name="lowStockOnly"/> filtrent sur le stock total
    /// agrégé (toutes entrepôts confondus). <paramref name="sortBy"/>/<paramref name="sortDescending"/>
    /// contrôlent le tri (nom par défaut).
    /// </summary>
    Task<(IReadOnlyList<Product> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize, string? search = null, int? minQuantity = null, int? maxQuantity = null,
        bool lowStockOnly = false, ProductSortBy sortBy = ProductSortBy.Name, bool sortDescending = false,
        CancellationToken ct = default);

    Task AddAsync(Product product, CancellationToken ct = default);
    void Update(Product product);
}
