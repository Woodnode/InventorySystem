using InventorySystem.Application.Common;
using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Dtos;
using MediatR;

namespace InventorySystem.Application.Movements.Queries;

/// <summary>
/// Cas d'usage : exporter l'historique des mouvements de stock en CSV ou Excel — soit
/// pour un seul produit, soit l'historique récent borné (<see cref="ExportLimits.MaxRows"/>).
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
            ? await _movements.ListByProductAsync(productId, ExportLimits.MaxRows, cancellationToken)
            : await _movements.ListAllAsync(ExportLimits.MaxRows, cancellationToken);

        var truncated = movements.Count >= ExportLimits.MaxRows;

        var productIds = movements.Select(m => m.ProductId).Distinct();
        var products = (await _products.ListByIdsAsync(productIds, cancellationToken))
            .ToDictionary(p => p.Id, p => p);

        var warehouseIds = movements
            .SelectMany(m => m.ToWarehouseId is Guid to
                ? new[] { m.WarehouseId, to }
                : new[] { m.WarehouseId })
            .Distinct();
        var warehouses = (await _warehouses.ListByIdsAsync(warehouseIds, cancellationToken))
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

        var file = await _export.ExportMovementsAsync(rows, request.Format, cancellationToken);
        if (!truncated)
            return file;

        var name = Path.GetFileNameWithoutExtension(file.FileName) + "_tronque" + Path.GetExtension(file.FileName);
        return file with { FileName = name, Truncated = true };
    }
}
