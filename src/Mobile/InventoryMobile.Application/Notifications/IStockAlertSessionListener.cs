namespace InventoryMobile.Application.Notifications;

/// <summary>
/// Écoute les alertes de stock bas pour toute la durée de la session (login/restauration de
/// session -> logout/expiration), pas seulement pendant que ProductsPage est affichée à
/// l'écran (voir AUDIT.md M-4). Implémentation dans le projet tête InventoryMobile (a besoin
/// de <c>MainThread</c>, indisponible dans Infrastructure — voir <see cref="ILocalNotificationService"/>
/// pour le même motif).
/// </summary>
public interface IStockAlertSessionListener
{
    /// <summary>Idempotent : sans effet si déjà démarré.</summary>
    Task StartAsync();

    /// <summary>Idempotent : sans effet si déjà arrêté.</summary>
    Task StopAsync();
}
