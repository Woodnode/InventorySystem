using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Common.Interfaces;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Liste bornée (référentiel — plafond <paramref name="maxRows"/>).</summary>
    Task<IReadOnlyList<Supplier>> ListAsync(int maxRows, CancellationToken ct = default);

    Task AddAsync(Supplier supplier, CancellationToken ct = default);
    void Update(Supplier supplier);
}
