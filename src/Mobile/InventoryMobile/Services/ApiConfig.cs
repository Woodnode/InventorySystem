using System.Reflection;

namespace InventoryMobile.Services;

/// <summary>
/// Résout l'URL de base de l'API selon la plateforme (plan §8.4).
/// DEBUG : HTTP local (certificat HTTPS de dev non approuvé sur émulateur).
/// RELEASE : HTTPS production — valeur injectée à la COMPILATION via la propriété MSBuild
/// <c>InventoryApiHost</c> (ex. <c>dotnet publish -p:InventoryApiHost=https://api.monentreprise.com</c>,
/// voir InventoryMobile.csproj), PAS lue depuis une variable d'environnement au runtime.
/// Une variable d'environnement OS n'existe pour aucun mécanisme sur un binaire Android/iOS
/// installé — un correctif précédent basé sur <c>Environment.GetEnvironmentVariable</c>
/// rendait donc toute build Release inutilisable sur ces plateformes (voir ré-audit).
/// </summary>
public static class ApiConfig
{
#if DEBUG
    private static string ResolveHost() => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:5244"
        : "http://localhost:5244";
#else
    private static string ResolveHost()
    {
        var fromAssembly = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "InventoryApiHost")?.Value;

        if (!string.IsNullOrWhiteSpace(fromAssembly))
            return fromAssembly.Trim().TrimEnd('/');

        // Pas d'hôte de production par défaut : un faux placeholder (ex.
        // api.inventory.example.com) laisserait une build Release silencieusement pointer
        // vers un serveur qui n'existe pas. Mieux vaut échouer bruyamment au démarrage
        // qu'échouer silencieusement sur chaque appel réseau.
        throw new InvalidOperationException(
            "La propriété MSBuild InventoryApiHost doit être fournie à la compilation pour " +
            "une build Release (ex. dotnet publish -p:InventoryApiHost=https://api.mondeploiement.com, " +
            "sans slash final) — voir InventoryMobile.csproj et ApiConfig.cs.");
    }
#endif

    private static string Host { get; } = ResolveHost();

    public static string BaseUrl => $"{Host}/api/v1/";

    /// <summary>Hub SignalR StockHub — mêmes contraintes réseau que <see cref="BaseUrl"/>.</summary>
    // Le hub est servi sous /api/v1, comme le reste de l'API : en production un seul relais
    // transmet les deux au serveur.
    public static string StockHubUrl => $"{Host}/api/v1/hubs/stock";
}
