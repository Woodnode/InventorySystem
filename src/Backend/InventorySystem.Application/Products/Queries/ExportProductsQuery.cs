using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using MediatR;

namespace InventorySystem.Application.Products.Queries;

/// <summary>
/// Cas d'usage : exporter le catalogue produits (avec stock total agrégé) en CSV ou Excel.
/// Même agrégation Product + Stock que <see cref="GetProductsQuery"/> (évite le N+1,
/// voir plan §5) ; seule la mise en forme finale diffère.
/// </summary>
public sealed record ExportProductsQuery(ExportFormat Format) : IRequest<ExportFileDto>;

public sealed class ExportProductsQueryHandler : IRequestHandler<ExportProductsQuery, ExportFileDto>
{
    private readonly IProductRepository _products;
    private readonly IStockRepository _stocks;
    private readonly IExportService _export;

    public ExportProductsQueryHandler(
        IProductRepository products, IStockRepository stocks, IExportService export)
    {
        _products = products;
        _stocks = stocks;
        _export = export;
    }

    public async Task<ExportFileDto> Handle(ExportProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _products.ListAsync(cancellationToken);
        var totals = await _stocks.GetTotalQuantitiesAsync(products.Select(p => p.Id), cancellationToken);

        var dtos = products
            .Select(p =>
            {
                var total = totals.GetValueOrDefault(p.Id, 0);
                return new ProductDto(
                    p.Id, p.Sku.Value, p.Name, p.Description, total, p.LowStockThreshold, p.IsLowOnStock(total));
            })
            .ToList();

        return _export.ExportProducts(dtos, request.Format);
    }
}
