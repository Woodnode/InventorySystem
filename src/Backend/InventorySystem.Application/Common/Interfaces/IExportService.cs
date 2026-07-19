using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Movements.Dtos;
using InventorySystem.Application.Products.Dtos;

namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Génère un export de données en CSV ou Excel. Implémenté dans Infrastructure (ClosedXML /
/// CsvHelper) — l'Application ne référence jamais ces bibliothèques directement (DIP, voir
/// plan §3.2/§4, même principe que <see cref="IIdentityService"/>).
/// </summary>
public interface IExportService
{
    ExportFileDto ExportProducts(IReadOnlyList<ProductDto> products, ExportFormat format);

    ExportFileDto ExportMovements(IReadOnlyList<MovementExportRow> movements, ExportFormat format);
}
