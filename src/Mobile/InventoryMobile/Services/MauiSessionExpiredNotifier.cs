using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Application.Notifications;

namespace InventoryMobile.Services;

/// <summary>
/// Sur expiration de session : coupe l'écoute SignalR app-wide puis redirige vers login.
/// </summary>
public sealed class MauiSessionExpiredNotifier : ISessionExpiredNotifier
{
    private readonly INavigationService _navigation;
    private readonly IStockAlertSessionListener _stockAlertSessionListener;

    public MauiSessionExpiredNotifier(
        INavigationService navigation,
        IStockAlertSessionListener stockAlertSessionListener)
    {
        _navigation = navigation;
        _stockAlertSessionListener = stockAlertSessionListener;
    }

    public void NotifySessionExpired()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await _stockAlertSessionListener.StopAsync();
            }
            catch
            {
                // Best-effort.
            }

            try
            {
                await _navigation.GoToAsync("//login");
            }
            catch
            {
                // Best-effort : ne jamais faire planter le handler HTTP.
            }
        });
    }
}
