using FluentValidation;
using InventorySystem.Application.Common.Interfaces;
using MediatR;

namespace InventorySystem.Application.Suppliers.Commands;

/// <summary>Modifie un fournisseur existant (voir AUDIT.md F-4 : le catalogue était create-only).</summary>
public sealed record UpdateSupplierCommand(Guid Id, string Name, string? ContactEmail, string? Phone) : IRequest;

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator(ISupplierRepository suppliers)
    {
        RuleFor(x => x.Id).MustAsync((id, ct) => suppliers.ExistsAsync(id, ct))
            .WithMessage("Fournisseur introuvable.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrEmpty(x.ContactEmail));
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}

public sealed class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand>
{
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSupplierCommandHandler(ISupplierRepository suppliers, IUnitOfWork unitOfWork)
    {
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.GetByIdAsync(request.Id, cancellationToken);
        supplier!.Update(request.Name, request.ContactEmail, request.Phone);

        _suppliers.Update(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
