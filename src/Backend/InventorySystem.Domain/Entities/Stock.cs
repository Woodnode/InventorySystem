using InventorySystem.Domain.Common;
using InventorySystem.Domain.Exceptions;

namespace InventorySystem.Domain.Entities;

/// <summary>
/// Quantité d'un <see cref="Product"/> disponible dans un <see cref="Warehouse"/> donné.
/// Aggregate root à part entière (clé métier : ProductId + WarehouseId, unique — voir
/// StockConfiguration) : Product reste un catalogue pur, il ne connaît pas sa quantité.
/// C'est ce découpage qui permet un vrai stock multi-entrepôt et des transferts (plan §3).
/// </summary>
public sealed class Stock : BaseEntity
{
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public int Quantity { get; private set; }

    /// <summary>
    /// Jeton de concurrence optimiste (mappé sur la colonne système Postgres xmin en
    /// Infrastructure — voir StockConfiguration). Deux mouvements concurrents sur le même
    /// (produit, entrepôt) ne doivent jamais s'écraser silencieusement.
    /// </summary>
    public uint Version { get; private set; }

    private Stock() { }

    private Stock(Guid productId, Guid warehouseId, int initialQuantity)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
        Quantity = initialQuantity;
    }

    public static Stock Create(Guid productId, Guid warehouseId, int initialQuantity = 0)
    {
        if (initialQuantity < 0)
            throw new DomainException("La quantité initiale ne peut pas être négative.");

        return new Stock(productId, warehouseId, initialQuantity);
    }

    /// <summary>Entrée de stock (réception, retour, ou moitié "arrivée" d'un transfert).</summary>
    public void Increase(int amount)
    {
        if (amount <= 0)
            throw new InvalidMovementException("La quantité d'une entrée doit être positive.");

        Quantity += amount;
        Touch();
    }

    /// <summary>
    /// Sortie de stock (vente, expédition, ou moitié "départ" d'un transfert).
    /// Ne peut jamais rendre le stock négatif — c'est l'invariant central de cet aggregate.
    /// </summary>
    public void Decrease(int amount)
    {
        if (amount <= 0)
            throw new InvalidMovementException("La quantité d'une sortie doit être positive.");
        if (amount > Quantity)
            throw new InsufficientStockException(Quantity, amount);

        Quantity -= amount;
        Touch();
    }
}
