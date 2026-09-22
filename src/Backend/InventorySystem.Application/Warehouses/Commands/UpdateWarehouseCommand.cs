using FluentValidation;
using InventorySystem.Application.Common.Interfaces;
using MediatR;

namespace InventorySystem.Application.Warehouses.Commands;

/// <summary>
/// Modifie nom/adresse d'un entrepôt existant (voir AUDIT.md F-4 : le catalogue était
/// create-only). Suppression volontairement absente : un entrepôt référencé par du stock
/// ou des mouvements historiques ne doit pas disparaître — voir <see cref="SetWarehouseActiveCommand"/>.
/// </summary>
public sealed record UpdateWarehouseCommand(Guid Id, string Name, string? Address) : IRequest;

public sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator(IWarehouseRepository warehouses)
    {
        RuleFor(x => x.Id).MustAsync((id, ct) => warehouses.ExistsAsync(id, ct))
            .WithMessage("Entrepôt introuvable.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWarehouseCommandHandler(IWarehouseRepository warehouses, IUnitOfWork unitOfWork)
    {
        _warehouses = warehouses;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        // La validation ci-dessus garantit déjà l'existence : non-null tolérable ici.
        var warehouse = await _warehouses.GetByIdAsync(request.Id, cancellationToken);
        warehouse!.Rename(request.Name, request.Address);

        _warehouses.Update(warehouse);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
