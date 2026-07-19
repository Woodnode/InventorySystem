namespace InventorySystem.Application.Suppliers.Dtos;

public sealed record SupplierDto(Guid Id, string Name, string? ContactEmail, string? Phone);
