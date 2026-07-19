namespace InventorySystem.Api.Contracts;

/// <summary>Corps HTTP de POST /warehouses — voir CreateProductRequest pour le principe.</summary>
public sealed record CreateWarehouseRequest(string Name, string? Address);
