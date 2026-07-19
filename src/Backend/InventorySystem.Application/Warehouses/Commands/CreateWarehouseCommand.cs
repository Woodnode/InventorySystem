using FluentValidation;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Entities;
using MediatR;

namespace InventorySystem.Application.Warehouses.Commands;

public sealed record CreateWarehouseCommand(string Name, string? Address) : IRequest<Guid>;

public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Guid>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWarehouseCommandHandler(IWarehouseRepository warehouses, IUnitOfWork unitOfWork)
    {
        _warehouses = warehouses;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = Warehouse.Create(request.Name, request.Address);

        await _warehouses.AddAsync(warehouse, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return warehouse.Id;
    }
}
