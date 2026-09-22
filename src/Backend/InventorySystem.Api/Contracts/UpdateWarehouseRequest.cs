namespace InventorySystem.Api.Contracts;

/// <summary>Corps HTTP de PUT /warehouses/{id}.</summary>
public sealed record UpdateWarehouseRequest(string Name, string? Address);

/// <summary>Corps HTTP de PATCH /warehouses/{id}/active.</summary>
public sealed record SetWarehouseActiveRequest(bool IsActive);
