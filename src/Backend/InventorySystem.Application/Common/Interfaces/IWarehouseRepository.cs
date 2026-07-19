using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Common.Interfaces;

public interface IWarehouseRepository
{
    Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Warehouse>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Warehouse warehouse, CancellationToken ct = default);
}
