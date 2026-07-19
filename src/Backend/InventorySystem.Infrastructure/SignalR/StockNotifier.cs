using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace InventorySystem.Infrastructure.SignalR;

/// <summary>
/// Implémentation de <see cref="IStockNotifier"/> via SignalR. Diffuse à tous les clients
/// connectés (voir plan §13 : simple et suffisant à cette échelle — pas de segmentation
/// par rôle/département, une alerte de stock bas est utile à toute l'équipe terrain).
/// </summary>
public sealed class StockNotifier : IStockNotifier
{
    private readonly IHubContext<StockHub> _hub;

    public StockNotifier(IHubContext<StockHub> hub) => _hub = hub;

    public async Task NotifyLowStockAsync(LowStockNotification notification, CancellationToken ct = default)
        => await _hub.Clients.All.SendAsync("LowStock", notification, ct);
}
