using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Contrat d'accès à l'agrégat StockMovement. Implémenté dans Infrastructure (DIP).
/// Un repository = un seul agrégat (voir SOLID §4 du plan) : distinct de IProductRepository
/// même si les deux vivent dans le même DbContext.
/// </summary>
public interface IStockMovementRepository
{
    Task AddAsync(StockMovement movement, CancellationToken ct = default);

    /// <summary>Vérifie l'idempotence d'un mouvement synchronisé depuis le mobile (plan §8.3).</summary>
    Task<bool> ExistsByClientGuidAsync(Guid clientGuid, CancellationToken ct = default);

    /// <summary>Liste complète, non paginée — réservée à l'export (CSV/Excel).</summary>
    Task<IReadOnlyList<StockMovement>> ListByProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Page d'historique, plus récents en premier — utilisée par les écrans web/mobile
    /// (l'historique grandit sans borne, contrairement aux listes de référence).</summary>
    Task<(IReadOnlyList<StockMovement> Items, int TotalCount)> ListByProductPagedAsync(
        Guid productId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Tous les mouvements, tous produits confondus (utilisé pour l'export global).</summary>
    Task<IReadOnlyList<StockMovement>> ListAllAsync(CancellationToken ct = default);
}
