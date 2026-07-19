using InventorySystem.Domain.Entities;

namespace InventorySystem.Application.Movements.Dtos;

/// <summary>DTO de sortie : jamais exposer l'entité Domain directement (voir plan §3.2).</summary>
public sealed record StockMovementDto(
    Guid Id,
    Guid ProductId,
    Guid WarehouseId,
    Guid? ToWarehouseId,
    MovementType Type,
    int Quantity,
    string? Reason,
    DateTime CreatedAtUtc);
