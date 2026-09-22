using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Persistence.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _db;

    public SupplierRepository(AppDbContext db) => _db = db;

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Suppliers.AnyAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Supplier>> ListAsync(int maxRows, CancellationToken ct = default)
        => await _db.Suppliers.AsNoTracking()
            .OrderBy(s => s.Name)
            .Take(maxRows)
            .ToListAsync(ct);

    public async Task AddAsync(Supplier supplier, CancellationToken ct = default)
        => await _db.Suppliers.AddAsync(supplier, ct);

    public void Update(Supplier supplier) => _db.Suppliers.Update(supplier);
}
