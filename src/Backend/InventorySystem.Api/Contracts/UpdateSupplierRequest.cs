namespace InventorySystem.Api.Contracts;

/// <summary>Corps HTTP de PUT /suppliers/{id}.</summary>
public sealed record UpdateSupplierRequest(string Name, string? ContactEmail, string? Phone);

/// <summary>Corps HTTP de PATCH /suppliers/{id}/active.</summary>
public sealed record SetSupplierActiveRequest(bool IsActive);
