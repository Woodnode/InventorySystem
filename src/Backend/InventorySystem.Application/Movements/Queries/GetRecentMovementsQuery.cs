using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Dtos;
using MediatR;

namespace InventorySystem.Application.Movements.Queries;

/// <summary>
/// Derniers mouvements, tous produits confondus — journal d'activité de l'entrepôt.
///
/// L'écran Mouvements restait vide tant qu'aucun produit n'était choisi, alors que la
/// question la plus courante est « que s'est-il passé récemment ? ».
/// </summary>
public sealed record GetRecentMovementsQuery(int Take = 20) : IRequest<IReadOnlyList<RecentMovementDto>>;

public sealed class GetRecentMovementsQueryHandler
    : IRequestHandler<GetRecentMovementsQuery, IReadOnlyList<RecentMovementDto>>
{
    private readonly IStockMovementRepository _movements;
    private readonly IProductRepository _products;
    private readonly IWarehouseRepository _warehouses;

    public GetRecentMovementsQueryHandler(
        IStockMovementRepository movements,
        IProductRepository products,
        IWarehouseRepository warehouses)
    {
        _movements = movements;
        _products = products;
        _warehouses = warehouses;
    }

    public async Task<IReadOnlyList<RecentMovementDto>> Handle(
        GetRecentMovementsQuery request, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 100);
        var mouvements = await _movements.ListAllAsync(take, cancellationToken);
        if (mouvements.Count == 0)
            return Array.Empty<RecentMovementDto>();

        // Les libellés ne sont chargés que pour les lignes affichées.
        var produits = (await _products.ListByIdsAsync(
                mouvements.Select(m => m.ProductId).Distinct(), cancellationToken))
            .ToDictionary(p => p.Id);

        var entrepots = (await _warehouses.ListAsync(
                Common.QueryLimits.ReferenceMaxRows, cancellationToken))
            .ToDictionary(w => w.Id, w => w.Name);

        return mouvements
            .Select(m =>
            {
                produits.TryGetValue(m.ProductId, out var produit);
                return new RecentMovementDto(
                    m.Id,
                    m.ProductId,
                    produit?.Sku.Value ?? "—",
                    produit?.Name ?? "Produit supprimé",
                    entrepots.GetValueOrDefault(m.WarehouseId, "—"),
                    m.ToWarehouseId is null ? null : entrepots.GetValueOrDefault(m.ToWarehouseId.Value, "—"),
                    m.Type,
                    m.Quantity,
                    m.Reason,
                    m.CreatedAtUtc);
            })
            .ToList();
    }
}
