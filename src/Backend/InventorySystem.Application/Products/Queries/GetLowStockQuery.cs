using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using MediatR;

namespace InventorySystem.Application.Products.Queries;

/// <summary>
/// Cas d'usage : lister les produits dont le stock total (tous entrepôts confondus)
/// est sous leur seuil de réappro. Agrège Product (catalogue) et Stock (quantités)
/// en une seule requête groupée côté repository pour éviter le N+1 (plan §5, async).
/// </summary>
public sealed record GetLowStockQuery : IRequest<IReadOnlyList<ProductDto>>;

public sealed class GetLowStockQueryHandler
    : IRequestHandler<GetLowStockQuery, IReadOnlyList<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly IStockRepository _stocks;

    public GetLowStockQueryHandler(IProductRepository products, IStockRepository stocks)
    {
        _products = products;
        _stocks = stocks;
    }

    public async Task<IReadOnlyList<ProductDto>> Handle(
        GetLowStockQuery request, CancellationToken cancellationToken)
    {
        var products = await _products.ListAsync(cancellationToken);
        var totals = await _stocks.GetTotalQuantitiesAsync(products.Select(p => p.Id), cancellationToken);

        return products
            .Select(p =>
            {
                var total = totals.GetValueOrDefault(p.Id, 0);
                return new ProductDto(
                    p.Id, p.Sku.Value, p.Name, p.Description,
                    total, p.LowStockThreshold, p.IsLowOnStock(total));
            })
            .Where(dto => dto.IsLowOnStock)
            .ToList();
    }
}
