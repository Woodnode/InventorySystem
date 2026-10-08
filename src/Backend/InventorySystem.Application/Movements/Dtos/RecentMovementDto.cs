using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Movements.Dtos;

/// <summary>
/// Ligne du journal global, enrichie des libellés.
///
/// Le DTO par produit ne porte que des identifiants : suffisant sur une fiche, où le
/// produit est déjà connu, mais illisible dans un journal qui brasse tout le catalogue.
/// </summary>
public sealed record RecentMovementDto(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string WarehouseName,
    string? ToWarehouseName,
    MovementType Type,
    int Quantity,
    string? Reason,
    DateTime CreatedAtUtc);
