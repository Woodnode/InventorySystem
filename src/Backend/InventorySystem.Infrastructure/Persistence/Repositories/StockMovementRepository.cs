using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Persistence.Repositories;

public sealed class StockMovementRepository : IStockMovementRepository
{
    private readonly AppDbContext _db;

    public StockMovementRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(StockMovement movement, CancellationToken ct = default)
        => await _db.StockMovements.AddAsync(movement, ct);

    public Task<bool> ExistsByClientGuidAsync(Guid clientGuid, CancellationToken ct = default)
        => _db.StockMovements.AnyAsync(m => m.ClientGuid == clientGuid, ct);

    public async Task<IReadOnlyList<StockMovement>> ListByProductAsync(
        Guid productId, int maxRows, CancellationToken ct = default)
        => await _db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(maxRows)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<StockMovement> Items, int TotalCount)> ListByProductPagedAsync(
        Guid productId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAtUtc);

        
        var totalCount = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<StockMovement>> ListAllAsync(int maxRows, CancellationToken ct = default)
        => await _db.StockMovements.AsNoTracking()
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(maxRows)
            .ToListAsync(ct);
}
