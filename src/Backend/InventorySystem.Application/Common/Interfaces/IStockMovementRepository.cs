using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Common.Interfaces;

/// <summary>Totaux d'une journee, tels que renvoyes par l'agregation SQL.</summary>
public sealed record DailyMovementTotals(DateTime Day, int InQuantity, int OutQuantity);

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

    /// <summary>
    /// Liste bornée pour un produit (export ciblé) — plafond <paramref name="maxRows"/>.
    /// </summary>
    Task<IReadOnlyList<StockMovement>> ListByProductAsync(
        Guid productId, int maxRows, CancellationToken ct = default);

    /// <summary>Page d'historique, plus récents en premier — utilisée par les écrans web/mobile
    /// (l'historique grandit sans borne, contrairement aux listes de référence).</summary>
    Task<(IReadOnlyList<StockMovement> Items, int TotalCount)> ListByProductPagedAsync(
        Guid productId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Liste bornée (plafond <paramref name="maxRows"/>), plus récents en premier —
    /// réservée à l'export global pour éviter de charger un historique illimité.
    /// </summary>
    Task<IReadOnlyList<StockMovement>> ListAllAsync(int maxRows, CancellationToken ct = default);

    /// <summary>
    /// Quantites entrees et sorties agregees par jour depuis <paramref name="sinceUtc"/>.
    ///
    /// L'agregation se fait en base : l'historique grandit sans borne, le ramener en
    /// memoire pour le regrouper cote serveur ne tiendrait pas dans la duree.
    /// Les transferts sont exclus — ils ne modifient pas le stock total detenu.
    /// </summary>
    Task<IReadOnlyList<DailyMovementTotals>> SumByDayAsync(
        DateTime sinceUtc, CancellationToken ct = default);
}
