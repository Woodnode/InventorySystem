using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Dtos;
using MediatR;

namespace InventorySystem.Application.Movements.Queries;

/// <summary>
/// Cas d'usage : lister l'historique des mouvements d'un produit (paginé — l'historique
/// grandit sans borne). Page/PageSize normalisés par <see cref="Pagination.Normalize"/>.
/// </summary>
public sealed record GetMovementsByProductQuery(
    Guid ProductId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<StockMovementDto>>;

public sealed class GetMovementsByProductQueryHandler
    : IRequestHandler<GetMovementsByProductQuery, PagedResult<StockMovementDto>>
{
    private readonly IStockMovementRepository _movements;

    public GetMovementsByProductQueryHandler(IStockMovementRepository movements) => _movements = movements;

    public async Task<PagedResult<StockMovementDto>> Handle(
        GetMovementsByProductQuery request, CancellationToken cancellationToken)
    {
        var (page, pageSize) = Pagination.Normalize(request.Page, request.PageSize);
        var (movements, totalCount) = await _movements.ListByProductPagedAsync(
            request.ProductId, page, pageSize, cancellationToken);

        var items = movements
            .Select(m => new StockMovementDto(
                m.Id, m.ProductId, m.WarehouseId, m.ToWarehouseId, m.Type, m.Quantity, m.Reason, m.CreatedAtUtc))
            .ToList();

        return new PagedResult<StockMovementDto>(items, totalCount, page, pageSize);
    }
}
