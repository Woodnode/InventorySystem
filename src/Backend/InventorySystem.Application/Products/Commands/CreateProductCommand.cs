using FluentValidation;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using MediatR;

namespace InventorySystem.Application.Products.Commands;

/// <summary>
/// Cas d'usage : créer un produit et son stock initial dans un entrepôt.
/// Un handler MediatR = un seul cas d'usage. Product (catalogue) et Stock (quantité par
/// entrepôt) sont deux aggregates distincts, mais créés ensemble ici pour l'UX : on ne
/// crée jamais un produit "flottant" sans savoir où il se trouve physiquement (plan §3).
/// </summary>
public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string? Description,
    int LowStockThreshold,
    Guid WarehouseId,
    int InitialQuantity,
    Guid? SupplierId,
    string? ProjectCode,
    string? Collection,
    string? VolumeNumber,
    string? ProductType,
    int? Year,
    decimal? WeightPerCopyLb,
    string? Company,
    string? Section,
    string? Space,
    string? Pallet,
    int BoxesCount,
    int CopiesPerBox,
    DateTime? EntryDate,
    DateTime? ExitDate,
    string? DistributorName,
    DateTime? ReturnDate,
    string? Comment) : IRequest<Guid>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator(IWarehouseRepository warehouses, ISupplierRepository suppliers)
    {
        RuleFor(x => x.Sku).NotEmpty().MinimumLength(3).MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InitialQuantity).GreaterThanOrEqualTo(0);

        RuleFor(x => x.ProjectCode).MaximumLength(200);
        RuleFor(x => x.Collection).MaximumLength(150);
        RuleFor(x => x.VolumeNumber).MaximumLength(50);
        RuleFor(x => x.ProductType).MaximumLength(100);
        RuleFor(x => x.Company).MaximumLength(150);

        RuleFor(x => x.Section).MaximumLength(50);
        RuleFor(x => x.Space).MaximumLength(50);
        RuleFor(x => x.Pallet).MaximumLength(50);
        RuleFor(x => x.DistributorName).MaximumLength(150);
        RuleFor(x => x.Comment).MaximumLength(1000);
        RuleFor(x => x.BoxesCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CopiesPerBox).GreaterThanOrEqualTo(0);

        RuleFor(x => x.WarehouseId)
            .MustAsync((id, ct) => warehouses.ExistsAsync(id, ct))
            .WithMessage("L'entrepôt spécifié n'existe pas.");

        RuleFor(x => x.SupplierId)
            .MustAsync((id, ct) => suppliers.ExistsAsync(id!.Value, ct))
            .When(x => x.SupplierId is not null)
            .WithMessage("Le fournisseur spécifié n'existe pas.");
    }
}

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _products;
    private readonly IStockRepository _stocks;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(IProductRepository products, IStockRepository stocks, IUnitOfWork unitOfWork)
    {
        _products = products;
        _stocks = stocks;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = Product.Create(
            Sku.Create(request.Sku),
            request.Name,
            request.Description,
            request.LowStockThreshold,
            request.SupplierId,
            request.ProjectCode,
            request.Collection,
            request.VolumeNumber,
            request.ProductType,
            request.Year,
            request.WeightPerCopyLb,
            request.Company);

        var stock = Stock.Create(product.Id, request.WarehouseId, request.InitialQuantity);
        
        stock.UpdateLogistics(
            request.Section, request.Space, request.Pallet,
            request.BoxesCount, request.CopiesPerBox,
            request.EntryDate, request.ExitDate,
            request.DistributorName, request.ReturnDate, request.Comment);

        await _products.AddAsync(product, cancellationToken);
        await _stocks.AddAsync(stock, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
