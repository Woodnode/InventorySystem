namespace InventorySystem.Api.Contracts;

/// <summary>
/// Corps HTTP de POST /products. Distinct de <see cref="Application.Products.Commands.CreateProductCommand"/> :
/// le contrat public n'expose que ce qu'un appelant est censé fournir. Si le Command interne
/// gagne un jour un champ technique (ex. audit), il ne devient pas automatiquement
/// client-assignable — il faut l'ajouter ici explicitement.
/// </summary>
public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string? Description,
    int LowStockThreshold,
    Guid WarehouseId,
    int InitialQuantity,
    Guid? SupplierId,
    string? ProjectCode,
    string? Collection,
    string? VolumeNumber,
    string? ProductType,
    int? Year,
    decimal? WeightPerCopyLb,
    string? Company,
    string? Section,
    string? Space,
    string? Pallet,
    int BoxesCount,
    int CopiesPerBox,
    DateTime? EntryDate,
    DateTime? ExitDate,
    string? DistributorName,
    DateTime? ReturnDate,
    string? Comment);
