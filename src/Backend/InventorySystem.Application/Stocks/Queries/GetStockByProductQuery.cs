using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Stocks.Dtos;
using MediatR;

namespace InventorySystem.Application.Stocks.Queries;

/// <summary>Cas d'usage : répartition du stock d'un produit entrepôt par entrepôt.</summary>
public sealed record GetStockByProductQuery(Guid ProductId) : IRequest<IReadOnlyList<StockDto>>;

public sealed class GetStockByProductQueryHandler
    : IRequestHandler<GetStockByProductQuery, IReadOnlyList<StockDto>>
{
    private readonly IStockRepository _stocks;

    public GetStockByProductQueryHandler(IStockRepository stocks) => _stocks = stocks;

    public async Task<IReadOnlyList<StockDto>> Handle(GetStockByProductQuery request, CancellationToken cancellationToken)
    {
        var stocks = await _stocks.ListByProductAsync(request.ProductId, cancellationToken);
        return stocks.Select(s => new StockDto(
            s.ProductId, s.WarehouseId, s.Quantity,
            s.Section, s.Space, s.Pallet,
            s.BoxesCount, s.CopiesPerBox,
            s.EntryDate, s.ExitDate,
            s.DistributorName, s.ReturnDate, s.Comment,
            s.InventoryDate, s.ResponsibleName)).ToList();
    }
}
