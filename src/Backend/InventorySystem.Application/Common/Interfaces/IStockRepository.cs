using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Common.Interfaces;

/// <summary>Ou se trouve l'essentiel du stock d'un produit.</summary>
public sealed record ProductLocation(Guid WarehouseId, string WarehouseName, string? Section, int Quantity);

/// <summary>Charge d'un entrepot : references distinctes et unites detenues.</summary>
public sealed record WarehouseLoad(Guid WarehouseId, string WarehouseName, int ProductCount, int TotalQuantity);

/// <summary>
/// Contrat d'accès à l'agrégat Stock (quantité d'un produit dans un entrepôt).
/// Distinct de IProductRepository : Product est un catalogue, Stock porte la quantité
/// (voir plan §3 et Domain.Entities.Stock).
/// </summary>
public interface IStockRepository
{
    Task<Stock?> GetAsync(Guid productId, Guid warehouseId, CancellationToken ct = default);

    /// <summary>Renvoie la ligne de stock existante, ou en crée une nouvelle à zéro (non persistée tant que SaveChanges n'a pas été appelé).</summary>
    Task<Stock> GetOrCreateAsync(Guid productId, Guid warehouseId, CancellationToken ct = default);

    Task AddAsync(Stock stock, CancellationToken ct = default);
    void Update(Stock stock);

    Task<IReadOnlyList<Stock>> ListByProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Quantité totale (tous entrepôts confondus) pour chaque produit demandé — une seule requête agrégée (évite le N+1).</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetTotalQuantitiesAsync(IEnumerable<Guid> productIds, CancellationToken ct = default);

    /// <summary>
    /// Emplacement principal de chaque produit : l'entrepot qui en detient le plus,
    /// avec sa section. Affiche dans la liste catalogue, ou l'information se trouvait
    /// jusqu'ici a un clic de distance sur la fiche.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ProductLocation>> GetPrimaryLocationsAsync(
        IEnumerable<Guid> productIds, CancellationToken ct = default);

    /// <summary>Quantites detenues par entrepot, tous produits confondus.</summary>
    Task<IReadOnlyList<WarehouseLoad>> GetTotalsByWarehouseAsync(CancellationToken ct = default);
}
