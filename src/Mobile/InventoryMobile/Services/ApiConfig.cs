namespace InventoryMobile.Services;

/// <summary>
/// Résout l'URL de base de l'API selon la plateforme (plan §8.4). Deux pièges MAUI classiques
/// évités ici :
///  - Sur l'émulateur Android, "localhost" pointe vers l'émulateur lui-même, pas la machine
///    hôte : il faut l'alias spécial 10.0.2.2.
///  - Le certificat HTTPS de développement ASP.NET Core n'est pas approuvé par défaut sur un
///    émulateur/simulateur, ce qui casse TLS. On utilise donc le profil HTTP (port 5244,
///    déjà exposé par launchSettings.json) pour ce jalon local/démo — jamais en production.
/// </summary>
public static class ApiConfig
{
    public static string BaseUrl => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:5244/api/v1/"
        : "http://localhost:5244/api/v1/";

    /// <summary>Hub SignalR StockHub (alertes de stock bas) — mêmes contraintes réseau que <see cref="BaseUrl"/>.</summary>
    public static string StockHubUrl => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:5244/hubs/stock"
        : "http://localhost:5244/hubs/stock";
}
