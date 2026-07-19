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
}
