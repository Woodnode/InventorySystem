using InventoryMobile.Application.Notifications;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace InventoryMobile.Services;

/// <summary>
/// Implémentation de <see cref="ILocalNotificationService"/> via Plugin.LocalNotification.
/// Vit dans le head MAUI, comme <see cref="SecureStorageTokenStore"/> : seul ce projet
/// référence les bindings natifs par plateforme du plugin.
/// </summary>
public sealed class PluginLocalNotificationService : ILocalNotificationService
{
    public async Task ShowLowStockAlertAsync(LowStockAlert alert)
    {
        var permission = new NotificationPermission();
        if (!await LocalNotificationCenter.Current.AreNotificationsEnabled(permission))
        {
            var granted = await LocalNotificationCenter.Current.RequestNotificationPermission(permission);
            if (!granted) return; // refusé par l'utilisateur — rien à afficher, pas une erreur
        }

        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            // GetHashCode() d'un Guid n'est pas parfaitement unique mais suffisant ici : une
            // collision fait juste remplacer une notification existante pour le même produit
            // plutôt que d'en empiler une nouvelle — comportement acceptable (pas de spam).
            NotificationId = alert.ProductId.GetHashCode(),
            Title = "Stock bas",
            Description = $"{alert.Name} ({alert.Sku}) : {alert.Quantity} unité(s) restante(s).",
        });
    }
}
