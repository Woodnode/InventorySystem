using FluentValidation;
using InventorySystem.Application.Common.Interfaces;
using MediatR;

namespace InventorySystem.Application.Suppliers.Commands;

/// <summary>
/// Active/désactive un fournisseur — remplace la suppression (voir ré-audit, parité avec
/// <c>SetWarehouseActiveCommand</c>) : un fournisseur référencé par des produits existants ne
/// doit jamais disparaître, seulement cesser d'apparaître dans les sélecteurs de création.
/// </summary>
public sealed record SetSupplierActiveCommand(Guid Id, bool IsActive) : IRequest;

public sealed class SetSupplierActiveCommandValidator : AbstractValidator<SetSupplierActiveCommand>
{
    public SetSupplierActiveCommandValidator(ISupplierRepository suppliers)
    {
        RuleFor(x => x.Id).MustAsync((id, ct) => suppliers.ExistsAsync(id, ct))
            .WithMessage("Fournisseur introuvable.");
    }
}

public sealed class SetSupplierActiveCommandHandler : IRequestHandler<SetSupplierActiveCommand>
{
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public SetSupplierActiveCommandHandler(ISupplierRepository suppliers, IUnitOfWork unitOfWork)
    {
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SetSupplierActiveCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.GetByIdAsync(request.Id, cancellationToken);

        if (request.IsActive) supplier!.Activate(); else supplier!.Deactivate();

        _suppliers.Update(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
