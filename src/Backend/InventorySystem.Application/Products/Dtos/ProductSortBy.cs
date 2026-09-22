namespace InventorySystem.Application.Products.Dtos;

/// <summary>Critère de tri pour la liste paginée des produits (voir GetProductsQuery).</summary>
public enum ProductSortBy
{
    Name = 1,
    Sku = 2,

    /// <summary>Stock total agrégé, toutes entrepôts confondus (voir Stock).</summary>
    Quantity = 3,
}
