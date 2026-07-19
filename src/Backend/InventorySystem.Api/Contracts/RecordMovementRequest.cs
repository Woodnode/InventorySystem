using InventorySystem.Domain.Entities;

namespace InventorySystem.Api.Contracts;

/// <summary>Corps HTTP de POST /movements — voir CreateProductRequest pour le principe.</summary>
public sealed record RecordMovementRequest(
    Guid ProductId,
    Guid WarehouseId,
    MovementType Type,
    int Quantity,
    string? Reason,
    Guid? ToWarehouseId,
    Guid? ClientGuid);
