namespace InventorySystem.Api.Contracts;

/// <summary>Corps HTTP de POST /suppliers — voir CreateProductRequest pour le principe.</summary>
public sealed record CreateSupplierRequest(string Name, string? ContactEmail, string? Phone);
