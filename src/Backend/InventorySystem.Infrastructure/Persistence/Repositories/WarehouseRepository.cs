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

    public async Task<IReadOnlyList<Warehouse>> ListAsync(CancellationToken ct = default)
        => await _db.Warehouses.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Warehouse warehouse, CancellationToken ct = default)
        => await _db.Warehouses.AddAsync(warehouse, ct);
}
