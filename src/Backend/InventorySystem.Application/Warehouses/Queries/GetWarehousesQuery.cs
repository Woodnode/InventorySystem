using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Warehouses.Dtos;
using MediatR;

namespace InventorySystem.Application.Warehouses.Queries;

public sealed record GetWarehousesQuery : IRequest<IReadOnlyList<WarehouseDto>>;

public sealed class GetWarehousesQueryHandler : IRequestHandler<GetWarehousesQuery, IReadOnlyList<WarehouseDto>>
{
    private readonly IWarehouseRepository _warehouses;

    public GetWarehousesQueryHandler(IWarehouseRepository warehouses) => _warehouses = warehouses;

    public async Task<IReadOnlyList<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        var warehouses = await _warehouses.ListAsync(cancellationToken);
        return warehouses.Select(w => new WarehouseDto(w.Id, w.Name, w.Address, w.IsActive)).ToList();
    }
}
