namespace InventoryMobile.Application.Notifications;

/// <summary>
/// Abstraction d'affichage de notification OS locale (implémentée par le head MAUI via
/// Plugin.LocalNotification — mêmes raisons de partition que <see cref="Auth.ITokenStore"/> :
/// bindings natifs par plateforme, seul le head project peut les référencer).
/// </summary>
public interface ILocalNotificationService
{
    Task ShowLowStockAlertAsync(LowStockAlert alert);
}
