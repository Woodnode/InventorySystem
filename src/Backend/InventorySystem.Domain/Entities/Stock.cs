using InventorySystem.Domain.Common;
using InventorySystem.Domain.Exceptions;

namespace InventorySystem.Domain.Entities;

/// <summary>
/// Quantité d'un <see cref="Product"/> disponible dans un <see cref="Warehouse"/> donné.
/// </summary>
public sealed class Stock : BaseEntity
{
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public int Quantity { get; private set; }

    // Champs de logistique et d'emplacement (Excel)
    public string? Section { get; private set; }
    public string? Space { get; private set; }
    public string? Pallet { get; private set; }
    public int BoxesCount { get; private set; }
    public int CopiesPerBox { get; private set; }
    public DateTime? EntryDate { get; private set; }
    public DateTime? ExitDate { get; private set; }
    public string? DistributorName { get; private set; }
    public DateTime? ReturnDate { get; private set; }
    public string? Comment { get; private set; }

    // Dernière prise d'inventaire physique pour cet emplacement (feuille "Inventaire" de
    // l'Excel, distincte de la feuille "ListeProduits" — voir ImportProductsCommand).
    public DateTime? InventoryDate { get; private set; }
    public string? ResponsibleName { get; private set; }

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

    public void UpdateLogistics(
        string? section, string? space, string? pallet, 
        int boxesCount, int copiesPerBox, 
        DateTime? entryDate, DateTime? exitDate, 
        string? distributorName, DateTime? returnDate, string? comment)
    {
        if (boxesCount < 0) throw new DomainException("Le nombre de boîtes ne peut pas être négatif.");
        if (copiesPerBox < 0) throw new DomainException("Le nombre de copies par boîte ne peut pas être négatif.");

        Section = section;
        Space = space;
        Pallet = pallet;
        BoxesCount = boxesCount;
        CopiesPerBox = copiesPerBox;
        EntryDate = entryDate;
        ExitDate = exitDate;
        DistributorName = distributorName;
        ReturnDate = returnDate;
        Comment = comment;

        Touch();
    }

    /// <summary>Enregistre le résultat d'une prise d'inventaire physique (feuille "Inventaire").</summary>
    public void RecordInventoryTake(DateTime? inventoryDate, string? responsibleName)
    {
        InventoryDate = inventoryDate;
        ResponsibleName = responsibleName;

        Touch();
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
