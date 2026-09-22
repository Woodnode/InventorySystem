using InventoryMobile.Application.Notifications;
using InventoryMobile.Infrastructure.Realtime;

namespace InventoryMobile.Services;

/// <summary>
/// Démarre la connexion SignalR et relaie les alertes de stock bas vers les notifications
/// locales pour toute la durée de la session — pas seulement pendant que ProductsPage est
/// affichée (voir AUDIT.md M-4). Singleton comme <see cref="IStockAlertHubClient"/> lui-même.
/// </summary>
public sealed class StockAlertSessionListener : IStockAlertSessionListener
{
    private readonly IStockAlertHubClient _hub;
    private readonly ILocalNotificationService _notifications;
    // Singleton avec état mutable (_listening) partagé par toute la session : sans verrou, deux
    // appels concurrents à StartAsync/StopAsync (ex. LoginViewModel.InitializeAsync et une
    // future notification push non marshalée sur le thread UI) pourraient tous deux voir
    // `_listening == false` et s'abonner chacun à LowStockAlertReceived, doublant les
    // notifications malgré le contrat "idempotent" promis par l'interface (voir ré-audit).
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _listening;

    public StockAlertSessionListener(IStockAlertHubClient hub, ILocalNotificationService notifications)
    {
        _hub = hub;
        _notifications = notifications;
    }

    public async Task StartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!_listening)
            {
                _hub.LowStockAlertReceived += OnLowStockAlertReceived;
                _listening = true;
            }
        }
        finally
        {
            _lock.Release();
        }

        try
        {
            await _hub.StartAsync();
        }
        catch (Exception)
        {
            // Best-effort : WithAutomaticReconnect() gère les coupures transitoires, sinon
            // retentera au prochain appel (ex. prochain écran qui redémarre la session).
        }
    }

    public async Task StopAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_listening)
            {
                _hub.LowStockAlertReceived -= OnLowStockAlertReceived;
                _listening = false;
            }
        }
        finally
        {
            _lock.Release();
        }

        try
        {
            await _hub.StopAsync();
        }
        catch (Exception)
        {
            // Best-effort, comme StartAsync.
        }
    }

    private async void OnLowStockAlertReceived(object? sender, LowStockAlert alert)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => _notifications.ShowLowStockAlertAsync(alert));
        }
        catch (Exception)
        {
            // async void : ne jamais laisser une exception remonter jusqu'au process.
        }
    }
}
