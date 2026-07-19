using InventoryMobile.Application.Notifications;

namespace InventoryMobile.Infrastructure.Realtime;

/// <summary>
/// Connexion temps réel au hub SignalR StockHub côté backend (event "LowStock"). Le mobile
/// n'écoute que pendant que l'app est active — voir <see cref="LowStockAlert"/>.
/// </summary>
public interface IStockAlertHubClient
{
    event EventHandler<LowStockAlert>? LowStockAlertReceived;

    Task StartAsync(CancellationToken ct = default);

    Task StopAsync();
}
