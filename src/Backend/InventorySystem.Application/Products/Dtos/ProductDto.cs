namespace InventorySystem.Application.Products.Dtos;

/// <summary>
/// DTO de sortie : jamais exposer l'entité Domain directement (voir plan §3.2).
/// </summary>
public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    int Quantity,
    int LowStockThreshold,
    bool IsLowOnStock,
    string? ProjectCode,
    string? Collection,
    string? VolumeNumber,
    string? ProductType,
    int? Year,
    decimal? WeightPerCopyLb,
    string? Company);
