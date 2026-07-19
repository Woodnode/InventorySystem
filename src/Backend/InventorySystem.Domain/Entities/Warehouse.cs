using InventorySystem.Domain.Common;
using InventorySystem.Domain.Exceptions;

namespace InventorySystem.Domain.Entities;

/// <summary>
/// Entrepôt physique. Aggregate root indépendant : le stock (voir <see cref="Stock"/>)
/// référence un Warehouse par Id plutôt que de vivre "dans" lui, pour éviter qu'un
/// entrepôt à fort volume de mouvements ne devienne un goulot de contention (plan §3).
/// </summary>
public sealed class Warehouse : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Warehouse() { }

    private Warehouse(string name, string? address)
    {
        Name = name;
        Address = address;
    }

    public static Warehouse Create(string name, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Le nom de l'entrepôt est obligatoire.");

        return new Warehouse(name, address);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
