namespace InventorySystem.Application.Stocks.Dtos;

/// <summary>Quantité d'un produit dans un entrepôt donné.</summary>
public sealed record StockDto(
    Guid ProductId, 
    Guid WarehouseId, 
    int Quantity,
    string? Section,
    string? Space,
    string? Pallet,
    int BoxesCount,
    int CopiesPerBox,
    DateTime? EntryDate,
    DateTime? ExitDate,
    string? DistributorName,
    DateTime? ReturnDate,
    string? Comment,
    DateTime? InventoryDate,
    string? ResponsibleName);
