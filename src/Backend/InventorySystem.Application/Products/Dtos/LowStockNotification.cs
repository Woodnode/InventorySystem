namespace InventorySystem.Application.Products.Dtos;

/// <summary>
/// Diffusée en temps réel (SignalR) quand un mouvement de type Sortie fait franchir
/// à un produit son seuil de stock bas. Voir <see cref="Common.Interfaces.IStockNotifier"/>.
/// </summary>
public sealed record LowStockNotification(
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    int LowStockThreshold);
