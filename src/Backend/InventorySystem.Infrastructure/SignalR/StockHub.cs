using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InventorySystem.Infrastructure.SignalR;

/// <summary>
/// Hub SignalR pour les alertes de stock en temps réel. Purement passif côté serveur
/// (les clients ne font qu'écouter l'événement "LowStock" — voir <see cref="StockNotifier"/>) ;
/// aucune méthode cliente à exposer pour l'instant.
/// Connexion réservée aux utilisateurs authentifiés (voir plan §6, mêmes policies que le
/// reste de l'API) ; mappé sur "/hubs/stock" dans Program.cs.
/// </summary>
[Authorize]
public sealed class StockHub : Hub
{
}
