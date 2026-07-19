using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using MediatR;

namespace InventorySystem.Application.Products.Queries;

/// <summary>
/// Cas d'usage : récupérer un produit par SKU, avec son stock total agrégé. Utilisé par le
/// scan mobile (le code scanné == le SKU du produit) — voir plan §8.1.
/// </summary>
public sealed record GetProductBySkuQuery(string Sku) : IRequest<ProductDto?>;

public sealed class GetProductBySkuQueryHandler : IRequestHandler<GetProductBySkuQuery, ProductDto?>
{
    private readonly IProductRepository _products;
    private readonly IStockRepository _stocks;

    public GetProductBySkuQueryHandler(IProductRepository products, IStockRepository stocks)
    {
        _products = products;
        _stocks = stocks;
    }

    public async Task<ProductDto?> Handle(GetProductBySkuQuery request, CancellationToken cancellationToken)
    {
        var product = await _products.GetBySkuAsync(request.Sku, cancellationToken);
        if (product is null) return null;

        var totals = await _stocks.GetTotalQuantitiesAsync(new[] { product.Id }, cancellationToken);
        var total = totals.GetValueOrDefault(product.Id, 0);

        return new ProductDto(
            product.Id, product.Sku.Value, product.Name, product.Description,
            total, product.LowStockThreshold, product.IsLowOnStock(total));
    }
}
