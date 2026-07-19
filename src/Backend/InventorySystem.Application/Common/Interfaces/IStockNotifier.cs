using InventorySystem.Application.Products.Dtos;

namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Diffuse une alerte de stock bas en temps réel. Implémenté dans Infrastructure via
/// SignalR (<c>StockHub</c>) — l'Application ne référence jamais SignalR directement
/// (DIP, voir plan §3.2/§4, même principe que <see cref="IExportService"/>).
/// </summary>
public interface IStockNotifier
{
    Task NotifyLowStockAsync(LowStockNotification notification, CancellationToken ct = default);
}
