using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Common.Interfaces;

public interface IWarehouseRepository
{
    Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Liste bornée (référentiel — plafond <paramref name="maxRows"/>).</summary>
    Task<IReadOnlyList<Warehouse>> ListAsync(int maxRows, CancellationToken ct = default);

    /// <summary>Résolution ciblée par ids (export mouvements).</summary>
    Task<IReadOnlyList<Warehouse>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task AddAsync(Warehouse warehouse, CancellationToken ct = default);
    void Update(Warehouse warehouse);
}
