namespace InventorySystem.Domain.Common;

/// <summary>
/// Classe de base pour toutes les entités du domaine.
/// Porte l'identifiant, les champs d'audit et la collection de domain events.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; protected set; } = DateTime.UtcNow;

    protected void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
