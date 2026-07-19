using FluentValidation;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;
using MediatR;

namespace InventorySystem.Application.Movements.Commands;

/// <summary>
/// Cas d'usage : enregistrer un mouvement de stock (entrée/sortie/transfert).
/// <paramref name="ToWarehouseId"/> n'est utilisé (et obligatoire) que pour un transfert.
/// <paramref name="ClientGuid"/> est optionnel côté web (généré ici si absent) et
/// obligatoire côté mobile, où il garantit l'idempotence de la synchro offline (plan §8.3).
/// </summary>
public sealed record RecordMovementCommand(
    Guid ProductId,
    Guid WarehouseId,
    MovementType Type,
    int Quantity,
    string? Reason,
    Guid? ToWarehouseId,
    Guid? ClientGuid) : IRequest<Guid>;

public sealed class RecordMovementCommandValidator : AbstractValidator<RecordMovementCommand>
{
    public RecordMovementCommandValidator(IProductRepository products, IWarehouseRepository warehouses)
    {
        RuleFor(x => x.ProductId).NotEmpty()
            .MustAsync((id, ct) => products.ExistsAsync(id, ct))
            .WithMessage("Produit introuvable.");

        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(500);

        RuleFor(x => x.WarehouseId)
            .MustAsync((id, ct) => warehouses.ExistsAsync(id, ct))
            .WithMessage("L'entrepôt source n'existe pas.");

        RuleFor(x => x.ToWarehouseId)
            .NotNull().WithMessage("Un transfert doit préciser l'entrepôt de destination.")
            .When(x => x.Type == MovementType.Transfer);

        RuleFor(x => x.ToWarehouseId)
            .Null().WithMessage("L'entrepôt de destination ne s'applique qu'à un transfert.")
            .When(x => x.Type != MovementType.Transfer);

        RuleFor(x => x.ToWarehouseId)
            .MustAsync((id, ct) => warehouses.ExistsAsync(id!.Value, ct))
            .When(x => x.Type == MovementType.Transfer && x.ToWarehouseId is not null)
            .WithMessage("L'entrepôt de destination n'existe pas.");

        RuleFor(x => x)
            .Must(x => x.ToWarehouseId != x.WarehouseId)
            .When(x => x.Type == MovementType.Transfer)
            .WithMessage("L'entrepôt de destination doit être différent de l'entrepôt source.");
    }
}

public sealed class RecordMovementCommandHandler : IRequestHandler<RecordMovementCommand, Guid>
{
    private readonly IStockRepository _stocks;
    private readonly IStockMovementRepository _movements;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockNotifier _notifier;

    public RecordMovementCommandHandler(
        IStockRepository stocks,
        IStockMovementRepository movements,
        IProductRepository products,
        IUnitOfWork unitOfWork,
        IStockNotifier notifier)
    {
        _stocks = stocks;
        _movements = movements;
        _products = products;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task<Guid> Handle(RecordMovementCommand request, CancellationToken cancellationToken)
    {
        // Idempotence : un mouvement déjà synchronisé (même ClientGuid) n'est jamais rejoué,
        // ce qui permet au mobile de renvoyer sans risque un mouvement après une coupure réseau.
        if (request.ClientGuid is { } clientGuid &&
            await _movements.ExistsByClientGuidAsync(clientGuid, cancellationToken))
        {
            throw new InvalidMovementException("Ce mouvement a déjà été synchronisé.");
        }

        // Un Transfert redistribue le stock d'un produit entre entrepôts sans changer sa
        // quantité totale ; une Entrée l'augmente forcément. Seule une Sortie peut donc faire
        // franchir le seuil de stock bas — on capture la quantité totale AVANT mutation pour
        // détecter précisément ce franchissement (et ne notifier qu'une fois, pas à chaque
        // sortie tant que le produit reste bas).
        int? totalBeforeOut = null;
        if (request.Type == MovementType.Out)
        {
            var totals = await _stocks.GetTotalQuantitiesAsync(
                new[] { request.ProductId }, cancellationToken);
            totalBeforeOut = totals.GetValueOrDefault(request.ProductId, 0);
        }

        var sourceStock = await _stocks.GetOrCreateAsync(request.ProductId, request.WarehouseId, cancellationToken);

        switch (request.Type)
        {
            case MovementType.In:
                sourceStock.Increase(request.Quantity);
                _stocks.Update(sourceStock);
                break;

            case MovementType.Out:
                sourceStock.Decrease(request.Quantity);
                _stocks.Update(sourceStock);
                break;

            case MovementType.Transfer:
                sourceStock.Decrease(request.Quantity);
                _stocks.Update(sourceStock);

                var destinationStock = await _stocks.GetOrCreateAsync(
                    request.ProductId, request.ToWarehouseId!.Value, cancellationToken);
                destinationStock.Increase(request.Quantity);
                _stocks.Update(destinationStock);
                break;

            default:
                throw new InvalidMovementException($"Type de mouvement non géré : {request.Type}.");
        }

        var movement = StockMovement.Record(
            request.ProductId, request.WarehouseId, request.Type, request.Quantity,
            request.Reason, request.ToWarehouseId, request.ClientGuid);

        await _movements.AddAsync(movement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (request.Type == MovementType.Out && totalBeforeOut is int before)
        {
            var totalAfter = before - request.Quantity;
            var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);

            // Notifie seulement au moment du franchissement (pas déjà bas avant, bas après) —
            // évite de spammer une alerte à chaque sortie tant que le produit reste sous le seuil.
            if (product is not null && !product.IsLowOnStock(before) && product.IsLowOnStock(totalAfter))
            {
                await _notifier.NotifyLowStockAsync(
                    new LowStockNotification(
                        product.Id, product.Sku.Value, product.Name, totalAfter, product.LowStockThreshold),
                    cancellationToken);
            }
        }

        return movement.Id;
    }
}
