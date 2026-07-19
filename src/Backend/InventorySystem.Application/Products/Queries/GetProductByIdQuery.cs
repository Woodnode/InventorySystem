using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using MediatR;

namespace InventorySystem.Application.Products.Queries;

/// <summary>
/// Cas d'usage : récupérer un produit par id, avec son stock total agrégé. Nécessaire
/// depuis que GET /products est paginé (voir GetProductsQuery) — la fiche produit ne peut
/// plus se contenter de chercher dans une page du cache liste, un produit hors de la page
/// courante serait introuvable à tort.
/// </summary>
public sealed record GetProductByIdQuery(Guid Id) : IRequest<ProductDto?>;

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly IProductRepository _products;
    private readonly IStockRepository _stocks;

    public GetProductByIdQueryHandler(IProductRepository products, IStockRepository stocks)
    {
        _products = products;
        _stocks = stocks;
    }

    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.Id, cancellationToken);
        if (product is null) return null;

        var totals = await _stocks.GetTotalQuantitiesAsync(new[] { product.Id }, cancellationToken);
        var total = totals.GetValueOrDefault(product.Id, 0);

        return new ProductDto(
            product.Id, product.Sku.Value, product.Name, product.Description,
            total, product.LowStockThreshold, product.IsLowOnStock(total));
    }
}
