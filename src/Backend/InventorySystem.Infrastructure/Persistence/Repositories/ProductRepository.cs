using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db) => _db = db;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
        => _db.Products.FirstOrDefaultAsync(p => p.Sku == Sku.Create(sku), ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Products.AnyAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken ct = default)
        => await _db.Products.AsNoTracking().ToListAsync(ct);

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking().OrderBy(p => p.Name);
        var totalCount = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, totalCount);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await _db.Products.AddAsync(product, ct);

    public void Update(Product product) => _db.Products.Update(product);
}
