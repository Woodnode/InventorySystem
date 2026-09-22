using InventoryMobile.Application.Notifications;

namespace InventoryMobile.Infrastructure.Realtime;

/// <summary>
/// Connexion temps réel au hub SignalR StockHub côté backend (event "LowStock"). Démarrée/
/// arrêtée pour toute la durée de la session (pas juste un écran) par
/// <c>IStockAlertSessionListener</c> — voir AUDIT.md M-4 et <see cref="LowStockAlert"/>.
/// </summary>
public interface IStockAlertHubClient
{
    event EventHandler<LowStockAlert>? LowStockAlertReceived;

    Task StartAsync(CancellationToken ct = default);

    Task StopAsync();
}
