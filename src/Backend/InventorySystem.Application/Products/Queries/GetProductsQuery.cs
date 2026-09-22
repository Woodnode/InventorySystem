using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using MediatR;

namespace InventorySystem.Application.Products.Queries;

/// <summary>
/// Cas d'usage : lister les produits (paginé — le catalogue grandit sans borne) avec leur
/// stock total agrégé. <paramref name="Page"/>/<paramref name="PageSize"/> sont normalisés
/// par <see cref="Pagination.Normalize"/> avant usage (valeurs par défaut : 1 / 20).
/// <paramref name="Search"/> filtre sur SKU ou nom (sous-chaîne, insensible à la casse).
/// <paramref name="MinQuantity"/>/<paramref name="MaxQuantity"/>/<paramref name="LowStockOnly"/>
/// filtrent sur le stock total agrégé (toutes entrepôts confondus). <paramref name="SortBy"/>/
/// <paramref name="SortDescending"/> contrôlent le tri (nom par défaut).
/// </summary>
public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    int? MinQuantity = null,
    int? MaxQuantity = null,
    bool LowStockOnly = false,
    ProductSortBy SortBy = ProductSortBy.Name,
    bool SortDescending = false) : IRequest<PagedResult<ProductDto>>;

public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly IStockRepository _stocks;

    public GetProductsQueryHandler(IProductRepository products, IStockRepository stocks)
    {
        _products = products;
        _stocks = stocks;
    }

    public async Task<PagedResult<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var (page, pageSize) = Pagination.Normalize(request.Page, request.PageSize);
        var (products, totalCount) = await _products.ListPagedAsync(
            page, pageSize, request.Search, request.MinQuantity, request.MaxQuantity, request.LowStockOnly,
            request.SortBy, request.SortDescending, cancellationToken);

        // Les totaux ne sont récupérés que pour les produits de la page courante — inutile
        // d'agréger tout le stock de tout le catalogue pour n'en afficher qu'une page.
        var totals = await _stocks.GetTotalQuantitiesAsync(products.Select(p => p.Id), cancellationToken);

        var items = products
            .Select(p =>
            {
                var total = totals.GetValueOrDefault(p.Id, 0);
                return new ProductDto(
                    p.Id, p.Sku.Value, p.Name, p.Description, total, p.LowStockThreshold, p.IsLowOnStock(total),
                    p.ProjectCode, p.Collection, p.VolumeNumber, p.ProductType, p.Year, p.WeightPerCopyLb, p.Company);
            })
            .ToList();

        return new PagedResult<ProductDto>(items, totalCount, page, pageSize);
    }
}
