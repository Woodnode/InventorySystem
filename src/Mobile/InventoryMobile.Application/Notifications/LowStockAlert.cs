namespace InventoryMobile.Application.Notifications;

/// <summary>Reflète LowStockNotification côté backend (event SignalR "LowStock").</summary>
public sealed record LowStockAlert(Guid ProductId, string Sku, string Name, int Quantity, int LowStockThreshold);
