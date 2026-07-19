using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Common.Interfaces;

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
}
