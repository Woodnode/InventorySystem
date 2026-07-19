using InventorySystem.Domain.Common;
using InventorySystem.Domain.Exceptions;

namespace InventorySystem.Domain.Entities;

public enum MovementType
{
    In = 1,        // Entrée (réception, retour)
    Out = 2,       // Sortie (vente, expédition)
    Transfer = 3   // Transfert entre deux entrepôts (WarehouseId -> ToWarehouseId)
}

/// <summary>
/// Trace immuable d'un mouvement de stock. Le <see cref="ClientGuid"/> assure
/// l'idempotence de la synchronisation offline (pattern Outbox, voir plan §8.3).
/// Pour un transfert, <see cref="WarehouseId"/> est l'entrepôt source et
/// <see cref="ToWarehouseId"/> l'entrepôt destination.
/// </summary>
public sealed class StockMovement : BaseEntity
{
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? ToWarehouseId { get; private set; }
    public MovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public string? Reason { get; private set; }

    /// <summary>GUID généré côté client mobile — garantit l'idempotence du sync.</summary>
    public Guid ClientGuid { get; private set; }

    private StockMovement() { }

    public static StockMovement Record(
        Guid productId,
        Guid warehouseId,
        MovementType type,
        int quantity,
        string? reason,
        Guid? toWarehouseId = null,
        Guid? clientGuid = null)
    {
        if (type == MovementType.Transfer)
        {
            if (toWarehouseId is null)
                throw new InvalidMovementException("Un transfert doit préciser l'entrepôt de destination.");
            if (toWarehouseId == warehouseId)
                throw new InvalidMovementException("L'entrepôt de destination doit être différent de l'entrepôt source.");
        }
        else if (toWarehouseId is not null)
        {
            throw new InvalidMovementException("L'entrepôt de destination ne s'applique qu'à un transfert.");
        }

        return new StockMovement
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            ToWarehouseId = toWarehouseId,
            Type = type,
            Quantity = quantity,
            Reason = reason,
            ClientGuid = clientGuid ?? Guid.NewGuid()
        };
    }
}
