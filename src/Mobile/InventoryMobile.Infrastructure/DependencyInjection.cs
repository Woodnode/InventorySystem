using System.Text.Json;
using System.Text.Json.Serialization;
using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Auth;
using InventoryMobile.Infrastructure.Persistence;
using InventoryMobile.Infrastructure.Realtime;
using InventoryMobile.Infrastructure.Sync;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace InventoryMobile.Infrastructure;

/// <summary>
/// Composition root de la couche Infrastructure (miroir de
/// InventorySystem.Infrastructure/DependencyInjection.cs côté backend) : le head MAUI
/// n'a besoin d'appeler qu'une seule méthode d'extension, sans connaître les détails
/// de câblage Refit/HttpClient.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInventoryInfrastructure(
        this IServiceCollection services, string apiBaseUrl, string localDatabasePath, string stockHubUrl)
    {
        // ASP.NET Core sérialise en camelCase + enums en texte (voir Program.cs backend) ;
        // le client doit matcher exactement le même contrat JSON.
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());

        // Enregistré via factory (pas juste AddTransient<AuthHeaderHandler>()) car il a
        // besoin de l'URL de base, qui n'est pas résolvable depuis le conteneur DI.
        services.AddTransient(sp => new AuthHeaderHandler(sp.GetRequiredService<ITokenStore>(), apiBaseUrl));

        services
            .AddRefitClient<IInventoryApi>(new RefitSettings
            {
                ContentSerializer = new SystemTextJsonContentSerializer(jsonOptions),
            })
            .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<AuthHeaderHandler>();

        services.AddSingleton<IAuthService, AuthService>();

        // File d'attente offline + service de synchronisation. Enregistrés en Singleton
        // comme IAuthService : une seule connexion SQLite/un seul état de synchronisation
        // pour la durée de vie de l'app.
        services.AddSingleton<ILocalMovementQueue>(_ => new SqliteLocalMovementQueue(localDatabasePath));
        services.AddSingleton<IMovementSyncService, MovementSyncService>();

        // Connexion temps réel au hub StockHub (alertes de stock bas) — voir StockNotifier
        // côté backend. Singleton : une seule connexion pour la durée de vie de l'app.
        services.AddSingleton<IStockAlertHubClient>(sp =>
            new SignalRStockAlertHubClient(stockHubUrl, sp.GetRequiredService<IAuthService>()));

        return services;
    }
}
