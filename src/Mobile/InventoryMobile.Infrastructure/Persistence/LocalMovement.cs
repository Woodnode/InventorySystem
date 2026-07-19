using SQLite;

namespace InventoryMobile.Infrastructure.Persistence;

/// <summary>
/// Mouvement stocké localement (SQLite). Pattern Outbox : chaque mouvement créé
/// offline porte un GUID client + un flag <see cref="IsSynced"/> (voir plan §8.2/§8.3).
/// Le GUID garantit l'idempotence lors de la synchronisation.
/// </summary>
[Table("local_movements")]
public sealed class LocalMovement
{
    [PrimaryKey]
    public Guid ClientGuid { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public Guid WarehouseId { get; set; }

    /// <summary>Entrepôt de destination — renseigné uniquement pour un mouvement de type Transfert (3).</summary>
    public Guid? ToWarehouseId { get; set; }

    /// <summary>1 = Entrée, 2 = Sortie, 3 = Transfert (miroir de MobileMovementType côté mobile).</summary>
    public int Type { get; set; }

    public int Quantity { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>false tant que le mouvement n'a pas été confirmé par le serveur.</summary>
    public bool IsSynced { get; set; }
}
