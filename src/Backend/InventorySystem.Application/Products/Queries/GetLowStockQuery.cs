using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using MediatR;

namespace InventorySystem.Application.Products.Queries;

/// <summary>
/// Produits sous seuil de réappro — paginé (agrégation SQL).
/// </summary>
public sealed record GetLowStockQuery(int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ProductDto>>;

public sealed class GetLowStockQueryHandler
    : IRequestHandler<GetLowStockQuery, PagedResult<ProductDto>>
{
    private readonly IProductRepository _products;

    public GetLowStockQueryHandler(IProductRepository products) => _products = products;

    public async Task<PagedResult<ProductDto>> Handle(
        GetLowStockQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (rows, totalCount) = await _products.ListLowStockPagedAsync(
            page, pageSize, cancellationToken);

        var items = rows
            .Select(r => new ProductDto(
                r.Product.Id,
                r.Product.Sku.Value,
                r.Product.Name,
                r.Product.Description,
                r.TotalQuantity,
                r.Product.LowStockThreshold,
                true,
                r.Product.ProjectCode,
                r.Product.Collection,
                r.Product.VolumeNumber,
                r.Product.ProductType,
                r.Product.Year,
                r.Product.WeightPerCopyLb,
                r.Product.Company))
            .ToList();

        return new PagedResult<ProductDto>(items, totalCount, page, pageSize);
    }
}
