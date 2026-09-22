using InventorySystem.Domain.Common;
using InventorySystem.Domain.Exceptions;
using InventorySystem.Domain.ValueObjects;

namespace InventorySystem.Domain.Entities;

/// <summary>
/// Agrégat racine "Produit" : catalogue pur (identité, description, seuil de réappro).
/// La quantité en stock n'est PAS portée ici — elle vit dans <see cref="Stock"/>,
/// répartie par entrepôt, pour permettre un vrai multi-entrepôt et des transferts
/// (voir plan §3). Aucune dépendance à EF Core ou à un framework.
/// </summary>
public sealed class Product : BaseEntity
{
    public Sku Sku { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int LowStockThreshold { get; private set; }
    public Guid? SupplierId { get; private set; }

    // Nouveaux champs liés à l'édition (Excel)
    public string? ProjectCode { get; private set; }
    public string? Collection { get; private set; }
    public string? VolumeNumber { get; private set; }
    public string? ProductType { get; private set; }
    public int? Year { get; private set; }
    public decimal? WeightPerCopyLb { get; private set; }
    public string? Company { get; private set; }

    // Constructeur privé requis par EF Core.
    private Product() { }

    private Product(Sku sku, string name, string? description, int lowStockThreshold, Guid? supplierId,
        string? projectCode, string? collection, string? volumeNumber, string? productType, int? year, decimal? weightPerCopyLb, string? company)
    {
        Sku = sku;
        Name = name;
        Description = description;
        LowStockThreshold = lowStockThreshold;
        SupplierId = supplierId;
        ProjectCode = projectCode;
        Collection = collection;
        VolumeNumber = volumeNumber;
        ProductType = productType;
        Year = year;
        WeightPerCopyLb = weightPerCopyLb;
        Company = company;
    }

    public static Product Create(Sku sku, string name, string? description, int lowStockThreshold, Guid? supplierId = null,
        string? projectCode = null, string? collection = null, string? volumeNumber = null, string? productType = null, int? year = null, decimal? weightPerCopyLb = null, string? company = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Le nom du produit est obligatoire.");
        if (lowStockThreshold < 0)
            throw new DomainException("Le seuil de stock bas ne peut pas être négatif.");

        return new Product(sku, name, description, lowStockThreshold, supplierId, projectCode, collection, volumeNumber, productType, year, weightPerCopyLb, company);
    }

    public void UpdateDetails(string name, string? description, int lowStockThreshold,
        string? projectCode = null, string? collection = null, string? volumeNumber = null,
        string? productType = null, int? year = null, decimal? weightPerCopyLb = null, string? company = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Le nom du produit est obligatoire.");
        if (lowStockThreshold < 0)
            throw new DomainException("Le seuil de stock bas ne peut pas être négatif.");

        Name = name;
        Description = description;
        LowStockThreshold = lowStockThreshold;
        ProjectCode = projectCode;
        Collection = collection;
        VolumeNumber = volumeNumber;
        ProductType = productType;
        Year = year;
        WeightPerCopyLb = weightPerCopyLb;
        Company = company;

        Touch();
    }

    public void AssignSupplier(Guid? supplierId)
    {
        SupplierId = supplierId;
        Touch();
    }

    /// <summary>Un total de stock (toutes entrepôts confondus) est-il sous le seuil ?</summary>
    public bool IsLowOnStock(int totalQuantity) => totalQuantity <= LowStockThreshold;
}
