namespace InventorySystem.Application.Stocks.Dtos;

/// <summary>Quantité d'un produit dans un entrepôt donné.</summary>
public sealed record StockDto(Guid ProductId, Guid WarehouseId, int Quantity);
