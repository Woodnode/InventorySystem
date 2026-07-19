using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Movements.Dtos;

/// <summary>
/// Ligne d'export d'un mouvement de stock, avec les noms de produit et d'entrepôt déjà
/// résolus (un export en GUID bruts n'a aucune valeur pour l'utilisateur final).
/// </summary>
public sealed record MovementExportRow(
    Guid Id,
    string ProductSku,
    string ProductName,
    string WarehouseName,
    string? ToWarehouseName,
    MovementType Type,
    int Quantity,
    string? Reason,
    DateTime CreatedAtUtc);
