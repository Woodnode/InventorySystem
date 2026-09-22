using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Movements.Dtos;
using InventorySystem.Application.Products.Dtos;

namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Génère un export de données en CSV ou Excel. Implémenté dans Infrastructure (ClosedXML /
/// CsvHelper) — l'Application ne référence jamais ces bibliothèques directement (DIP).
/// </summary>
public interface IExportService
{
    Task<ExportFileDto> ExportProductsAsync(
        IReadOnlyList<ProductDto> products, ExportFormat format, CancellationToken ct = default);

    Task<ExportFileDto> ExportMovementsAsync(
        IReadOnlyList<MovementExportRow> movements, ExportFormat format, CancellationToken ct = default);
}
