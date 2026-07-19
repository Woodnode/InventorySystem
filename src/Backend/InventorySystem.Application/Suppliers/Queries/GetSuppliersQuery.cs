using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Suppliers.Dtos;
using MediatR;

namespace InventorySystem.Application.Suppliers.Queries;

public sealed record GetSuppliersQuery : IRequest<IReadOnlyList<SupplierDto>>;

public sealed class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    private readonly ISupplierRepository _suppliers;

    public GetSuppliersQueryHandler(ISupplierRepository suppliers) => _suppliers = suppliers;

    public async Task<IReadOnlyList<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var suppliers = await _suppliers.ListAsync(cancellationToken);
        return suppliers.Select(s => new SupplierDto(s.Id, s.Name, s.ContactEmail, s.Phone)).ToList();
    }
}
