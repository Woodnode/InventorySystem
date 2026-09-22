using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Persistence.Repositories;

public sealed class WarehouseRepository : IWarehouseRepository
{
    private readonly AppDbContext _db;

    public WarehouseRepository(AppDbContext db) => _db = db;

    public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Warehouses.AnyAsync(w => w.Id == id, ct);

    public async Task<IReadOnlyList<Warehouse>> ListAsync(int maxRows, CancellationToken ct = default)
        => await _db.Warehouses.AsNoTracking()
            .OrderBy(w => w.Name)
            .Take(maxRows)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Warehouse>> ListByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await _db.Warehouses.AsNoTracking()
            .Where(w => idList.Contains(w.Id))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Warehouse warehouse, CancellationToken ct = default)
        => await _db.Warehouses.AddAsync(warehouse, ct);

    public void Update(Warehouse warehouse) => _db.Warehouses.Update(warehouse);
}
