using ClosedXML.Excel;
using FluentAssertions;
using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Movements.Dtos;
using InventorySystem.Application.Products.Dtos;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Export;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Infrastructure.Export;

/// <summary>
/// Vérifie le contenu réel produit par <see cref="ExportService"/> (CSV et Excel) — non
/// couvert avant (voir AUDIT.md B-T1). Le compromis <c>Task.Run</c> (B-A1) n'est pas testé
/// ici : il n'affecte pas le contenu produit, seulement le thread d'exécution.
/// </summary>
public sealed class ExportServiceTests
{
    private readonly ExportService _sut = new();

    private static ProductDto NewProduct(string sku, bool lowOnStock)
        => new(Guid.NewGuid(), sku, $"Produit {sku}", null, lowOnStock ? 1 : 10, 5, lowOnStock, null, null, null, null, null, null, null);

    [Fact]
    public async Task ExportProductsAsync_Csv_ContainsHeaderAndOneLinePerProduct()
    {
        var products = new[] { NewProduct("SKU-1", lowOnStock: false), NewProduct("SKU-2", lowOnStock: true) };

        var file = await _sut.ExportProductsAsync(products, ExportFormat.Csv);

        file.ContentType.Should().Be("text/csv");
        file.FileName.Should().EndWith(".csv");

        using var reader = new StreamReader(file.Content);
        var lines = (await reader.ReadToEndAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        lines.Should().HaveCount(3); // en-tête + 2 produits
        lines[1].Should().Contain("SKU-1");
        lines[2].Should().Contain("SKU-2");
    }

    [Fact]
    public async Task ExportProductsAsync_Excel_ContainsHeaderRowAndOneRowPerProduct()
    {
        var products = new[] { NewProduct("SKU-1", lowOnStock: false), NewProduct("SKU-2", lowOnStock: true) };

        var file = await _sut.ExportProductsAsync(products, ExportFormat.Excel);

        file.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        file.FileName.Should().EndWith(".xlsx");

        using var workbook = new XLWorkbook(file.Content);
        var sheet = workbook.Worksheet("Produits");
        sheet.Cell(1, 1).GetString().Should().Be("SKU");
        sheet.Cell(2, 1).GetString().Should().Be("SKU-1");
        sheet.Cell(3, 1).GetString().Should().Be("SKU-2");
        sheet.Cell(4, 1).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public async Task ExportMovementsAsync_Csv_TranslatesMovementTypeToFrench()
    {
        var movement = new MovementExportRow(
            Guid.NewGuid(), "SKU-1", "Produit 1", "Entrepôt A", null,
            MovementType.In, 5, "Réception", DateTime.UtcNow);

        var file = await _sut.ExportMovementsAsync(new[] { movement }, ExportFormat.Csv);

        using var reader = new StreamReader(file.Content);
        var content = await reader.ReadToEndAsync();

        content.Should().Contain("Entrée"); // MovementType.In traduit, pas "In" brut
    }
}
