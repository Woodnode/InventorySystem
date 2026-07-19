namespace InventorySystem.Application.Warehouses.Dtos;

public sealed record WarehouseDto(Guid Id, string Name, string? Address, bool IsActive);
