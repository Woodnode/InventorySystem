namespace InventoryMobile.Application.Auth;

/// <summary>
/// Notifié quand la session devient invalide (refresh échoué / 401) pour renvoyer
/// l'utilisateur au login. Implémenté dans le head MAUI (navigation Shell).
/// </summary>
public interface ISessionExpiredNotifier
{
    void NotifySessionExpired();
}
