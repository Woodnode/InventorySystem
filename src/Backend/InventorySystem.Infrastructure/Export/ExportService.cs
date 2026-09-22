using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Dtos;
using InventorySystem.Application.Products.Dtos;

namespace InventorySystem.Infrastructure.Export;

/// <summary>
/// Export CSV/Excel. Renvoie un <see cref="Stream"/> (pas de double buffer <c>byte[]</c>).
/// CSV écrit en flux ; Excel (ClosedXML) reste borné par <c>ExportLimits.MaxRows</c>.
/// </summary>
/// <remarks>
/// <c>Task.Run</c> ci-dessous (AUDIT.md B-A1) : ClosedXML et CsvHelper n'exposent aucune API
/// async (E/S synchrone en mémoire, pas de vrai I/O réseau/disque à recouvrir), donc il n'y a
/// pas de "vrai async" possible ici sans changer de bibliothèque. <c>Task.Run</c> reste un
/// compromis pragmatique : il libère le thread de la requête ASP.NET Core pendant que le
/// travail CPU-bound (borné à <c>ExportLimits.MaxRows</c> lignes) tourne sur le thread pool,
/// plutôt que de bloquer le thread de requête directement. Accepté comme tel plutôt que
/// "corrigé" — remplacer ClosedXML/CsvHelper par une alternative streaming async serait un
/// changement de dépendance disproportionné pour ce gain.
/// </remarks>
public sealed class ExportService : IExportService
{
    private const string CsvContentType = "text/csv";
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public Task<ExportFileDto> ExportProductsAsync(
        IReadOnlyList<ProductDto> products, ExportFormat format, CancellationToken ct = default)
        => Task.Run(() => ExportProducts(products, format), ct);

    public Task<ExportFileDto> ExportMovementsAsync(
        IReadOnlyList<MovementExportRow> movements, ExportFormat format, CancellationToken ct = default)
        => Task.Run(() => ExportMovements(movements, format), ct);

    private static ExportFileDto ExportProducts(IReadOnlyList<ProductDto> products, ExportFormat format)
    {
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (format == ExportFormat.Excel)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Produits");

            sheet.Cell(1, 1).Value = "SKU";
            sheet.Cell(1, 2).Value = "Nom";
            sheet.Cell(1, 3).Value = "Description";
            sheet.Cell(1, 4).Value = "Quantité totale";
            sheet.Cell(1, 5).Value = "Seuil de stock bas";
            sheet.Cell(1, 6).Value = "Stock bas ?";
            sheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var p in products)
            {
                sheet.Cell(row, 1).Value = p.Sku;
                sheet.Cell(row, 2).Value = p.Name;
                sheet.Cell(row, 3).Value = p.Description ?? string.Empty;
                sheet.Cell(row, 4).Value = p.Quantity;
                sheet.Cell(row, 5).Value = p.LowStockThreshold;
                sheet.Cell(row, 6).Value = p.IsLowOnStock ? "Oui" : "Non";
                row++;
            }

            sheet.Columns().AdjustToContents();

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return new ExportFileDto(stream, ExcelContentType, $"produits_{stamp}.xlsx");
        }

        var csvRecords = products.Select(p => new
        {
            Sku = p.Sku,
            Nom = p.Name,
            Description = p.Description ?? string.Empty,
            QuantiteTotale = p.Quantity,
            SeuilStockBas = p.LowStockThreshold,
            StockBas = p.IsLowOnStock ? "Oui" : "Non",
        });

        return new ExportFileDto(WriteCsv(csvRecords), CsvContentType, $"produits_{stamp}.csv");
    }

    private static ExportFileDto ExportMovements(IReadOnlyList<MovementExportRow> movements, ExportFormat format)
    {
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (format == ExportFormat.Excel)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Mouvements");

            sheet.Cell(1, 1).Value = "SKU produit";
            sheet.Cell(1, 2).Value = "Nom produit";
            sheet.Cell(1, 3).Value = "Type";
            sheet.Cell(1, 4).Value = "Entrepôt source";
            sheet.Cell(1, 5).Value = "Entrepôt destination";
            sheet.Cell(1, 6).Value = "Quantité";
            sheet.Cell(1, 7).Value = "Motif";
            sheet.Cell(1, 8).Value = "Date (UTC)";
            sheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var m in movements)
            {
                sheet.Cell(row, 1).Value = m.ProductSku;
                sheet.Cell(row, 2).Value = m.ProductName;
                sheet.Cell(row, 3).Value = TranslateMovementType(m.Type);
                sheet.Cell(row, 4).Value = m.WarehouseName;
                sheet.Cell(row, 5).Value = m.ToWarehouseName ?? string.Empty;
                sheet.Cell(row, 6).Value = m.Quantity;
                sheet.Cell(row, 7).Value = m.Reason ?? string.Empty;
                var dateCell = sheet.Cell(row, 8);
                dateCell.Value = m.CreatedAtUtc;
                dateCell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                row++;
            }

            sheet.Columns().AdjustToContents();

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return new ExportFileDto(stream, ExcelContentType, $"mouvements_{stamp}.xlsx");
        }

        var csvRecords = movements.Select(m => new
        {
            SkuProduit = m.ProductSku,
            NomProduit = m.ProductName,
            Type = TranslateMovementType(m.Type),
            EntrepotSource = m.WarehouseName,
            EntrepotDestination = m.ToWarehouseName ?? string.Empty,
            Quantite = m.Quantity,
            Motif = m.Reason ?? string.Empty,
            DateUtc = m.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        });

        return new ExportFileDto(WriteCsv(csvRecords), CsvContentType, $"mouvements_{stamp}.csv");
    }

    private static string TranslateMovementType(Domain.Entities.MovementType type) => type switch
    {
        Domain.Entities.MovementType.In => "Entrée",
        Domain.Entities.MovementType.Out => "Sortie",
        Domain.Entities.MovementType.Transfer => "Transfert",
        _ => type.ToString(),
    };

    private static MemoryStream WriteCsv<T>(IEnumerable<T> records)
    {
        var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            csv.WriteRecords(records);
        }

        stream.Position = 0;
        return stream;
    }
}
