using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Persistence.Repositories;

public sealed class StockRepository : IStockRepository
{
    private readonly AppDbContext _db;

    public StockRepository(AppDbContext db) => _db = db;

    public Task<Stock?> GetAsync(Guid productId, Guid warehouseId, CancellationToken ct = default)
        => _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId, ct);

    public async Task<Stock> GetOrCreateAsync(Guid productId, Guid warehouseId, CancellationToken ct = default)
    {
        var existing = await GetAsync(productId, warehouseId, ct);
        if (existing is not null)
            return existing;

        var created = Stock.Create(productId, warehouseId);
        // Suivi immédiatement comme "Added" : Update() n'aura rien à refaire ensuite
        // (voir Update ci-dessous) — évite de transformer un INSERT en UPDATE à vide.
        await _db.Stocks.AddAsync(created, ct);
        return created;
    }

    public async Task AddAsync(Stock stock, CancellationToken ct = default)
        => await _db.Stocks.AddAsync(stock, ct);

    public void Update(Stock stock)
    {
        // Si l'entité est déjà suivie (issue de GetAsync/GetOrCreateAsync), le change
        // tracker EF Core a déjà capté les mutations (Increase/Decrease) — appeler
        // Update() dessus ne ferait que remarquer l'état sans rien changer. On ne
        // rattache explicitement que si elle est détachée (cas défensif).
        if (_db.Entry(stock).State == EntityState.Detached)
            _db.Stocks.Update(stock);
    }

    public async Task<IReadOnlyList<Stock>> ListByProductAsync(Guid productId, CancellationToken ct = default)
        => await _db.Stocks.AsNoTracking().Where(s => s.ProductId == productId).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> GetTotalQuantitiesAsync(
        IEnumerable<Guid> productIds, CancellationToken ct = default)
    {
        var ids = productIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, int>();

        return await _db.Stocks
            .AsNoTracking()
            .Where(s => ids.Contains(s.ProductId))
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Total, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, ProductLocation>> GetPrimaryLocationsAsync(
        IEnumerable<Guid> productIds, CancellationToken ct = default)
    {
        var ids = productIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, ProductLocation>();

        // Une ligne par couple produit/entrepot, jointe au nom de l'entrepot. Le choix
        // de l'emplacement principal se fait ensuite en memoire : la page ne compte
        // qu'une vingtaine de produits, et un GroupBy avec selection du maximum se
        // traduit mal en SQL pour un gain nul a cette echelle.
        var lignes = await (
            from s in _db.Stocks.AsNoTracking()
            join w in _db.Warehouses.AsNoTracking() on s.WarehouseId equals w.Id
            where ids.Contains(s.ProductId) && s.Quantity > 0
            select new { s.ProductId, s.WarehouseId, w.Name, s.Section, s.Quantity }
        ).ToListAsync(ct);

        return lignes
            .GroupBy(l => l.ProductId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var principal = g.OrderByDescending(x => x.Quantity).First();
                    return new ProductLocation(
                        principal.WarehouseId, principal.Name, principal.Section, principal.Quantity);
                });
    }

    public async Task<IReadOnlyList<WarehouseLoad>> GetTotalsByWarehouseAsync(CancellationToken ct = default)
    {
        var lignes = await (
            from s in _db.Stocks.AsNoTracking()
            join w in _db.Warehouses.AsNoTracking() on s.WarehouseId equals w.Id
            group new { s, w } by new { s.WarehouseId, w.Name } into g
            select new
            {
                g.Key.WarehouseId,
                g.Key.Name,
                // Un produit sans unite occupe une fiche mais pas de place : il ne
                // compte pas dans les references detenues.
                ProductCount = g.Count(x => x.s.Quantity > 0),
                TotalQuantity = g.Sum(x => x.s.Quantity),
            }
        ).ToListAsync(ct);

        return lignes
            .Select(l => new WarehouseLoad(l.WarehouseId, l.Name, l.ProductCount, l.TotalQuantity))
            .OrderByDescending(l => l.TotalQuantity)
            .ToList();
    }
}
