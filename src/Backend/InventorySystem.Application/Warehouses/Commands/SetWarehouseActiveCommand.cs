using FluentValidation;
using InventorySystem.Application.Common.Interfaces;
using MediatR;

namespace InventorySystem.Application.Warehouses.Commands;

/// <summary>
/// Active/désactive un entrepôt — remplace la suppression (voir AUDIT.md F-4) : un entrepôt
/// désactivé disparaît des sélecteurs de saisie sans casser l'historique des mouvements qui
/// le référencent encore par Id.
/// </summary>
public sealed record SetWarehouseActiveCommand(Guid Id, bool IsActive) : IRequest;

public sealed class SetWarehouseActiveCommandValidator : AbstractValidator<SetWarehouseActiveCommand>
{
    public SetWarehouseActiveCommandValidator(IWarehouseRepository warehouses)
    {
        RuleFor(x => x.Id).MustAsync((id, ct) => warehouses.ExistsAsync(id, ct))
            .WithMessage("Entrepôt introuvable.");
    }
}

public sealed class SetWarehouseActiveCommandHandler : IRequestHandler<SetWarehouseActiveCommand>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly IUnitOfWork _unitOfWork;

    public SetWarehouseActiveCommandHandler(IWarehouseRepository warehouses, IUnitOfWork unitOfWork)
    {
        _warehouses = warehouses;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SetWarehouseActiveCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouses.GetByIdAsync(request.Id, cancellationToken);

        if (request.IsActive) warehouse!.Activate(); else warehouse!.Deactivate();

        _warehouses.Update(warehouse);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
