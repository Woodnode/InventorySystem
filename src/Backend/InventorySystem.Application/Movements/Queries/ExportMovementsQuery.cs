using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Dtos;
using InventorySystem.Domain.Entities;
using MediatR;

namespace InventorySystem.Application.Movements.Queries;

/// <summary>
/// Cas d'usage : exporter l'historique des mouvements de stock en CSV ou Excel — soit
/// pour un seul produit (<see cref="ProductId"/> renseigné, même périmètre que
/// <see cref="GetMovementsByProductQuery"/>), soit l'historique complet.
/// Résout les noms de produit et d'entrepôt (l'agrégat StockMovement ne porte que des
/// Guid — voir Domain) en une seule passe, sans requête N+1 par ligne.
/// </summary>
public sealed record ExportMovementsQuery(ExportFormat Format, Guid? ProductId = null)
    : IRequest<ExportFileDto>;

public sealed class ExportMovementsQueryHandler : IRequestHandler<ExportMovementsQuery, ExportFileDto>
{
    private readonly IStockMovementRepository _movements;
    private readonly IProductRepository _products;
    private readonly IWarehouseRepository _warehouses;
    private readonly IExportService _export;

    public ExportMovementsQueryHandler(
        IStockMovementRepository movements,
        IProductRepository products,
        IWarehouseRepository warehouses,
        IExportService export)
    {
        _movements = movements;
        _products = products;
        _warehouses = warehouses;
        _export = export;
    }

    public async Task<ExportFileDto> Handle(ExportMovementsQuery request, CancellationToken cancellationToken)
    {
        var movements = request.ProductId is Guid productId
            ? await _movements.ListByProductAsync(productId, cancellationToken)
            : await _movements.ListAllAsync(cancellationToken);

        // Résolution des noms en une seule passe (deux requêtes au total, pas une par ligne).
        var products = (await _products.ListAsync(cancellationToken))
            .ToDictionary(p => p.Id, p => p);
        var warehouses = (await _warehouses.ListAsync(cancellationToken))
            .ToDictionary(w => w.Id, w => w.Name);

        var rows = movements
            .Select(m =>
            {
                var product = products.GetValueOrDefault(m.ProductId);
                return new MovementExportRow(
                    m.Id,
                    product?.Sku.Value ?? "(produit supprimé)",
                    product?.Name ?? "(produit supprimé)",
                    warehouses.GetValueOrDefault(m.WarehouseId, "(entrepôt supprimé)"),
                    m.ToWarehouseId is Guid toId ? warehouses.GetValueOrDefault(toId, "(entrepôt supprimé)") : null,
                    m.Type,
                    m.Quantity,
                    m.Reason,
                    m.CreatedAtUtc);
            })
            .ToList();

        return _export.ExportMovements(rows, request.Format);
    }
}
