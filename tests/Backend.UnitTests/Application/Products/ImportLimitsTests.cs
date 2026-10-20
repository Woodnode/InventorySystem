using System.IO.Compression;
using System.Text;
using FluentAssertions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Commands;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Products;

/// <summary>
/// Plafonds de l'import : un fichier hors limites est refusé avant tout traitement, sans
/// toucher à la base.
/// </summary>
public sealed class ImportLimitsTests
{
    private readonly Mock<IProductRepository> _products = new(MockBehavior.Strict);
    private readonly Mock<IStockRepository> _stocks = new(MockBehavior.Strict);
    private readonly Mock<IWarehouseRepository> _warehouses = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    private ImportProductsCommandHandler Handler() =>
        new(_products.Object, _stocks.Object, _warehouses.Object, _unitOfWork.Object);

    [Fact]
    public async Task EtantDonneCsvTropLong_QuandImporter_AlorsRefuseSansToucherALaBase()
    {
        var csv = new StringBuilder("Code produit;Titre\n");
        for (var i = 0; i <= ImportLimits.MaxRows; i++)
            csv.Append("SKU-").Append(i).Append(";Livre ").Append(i).Append('\n');
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        var result = await Handler().Handle(new ImportProductsCommand(stream, "catalogue.csv"), default);

        result.ProductsCreated.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().Contain("Trop de lignes");
    }

    [Fact]
    public async Task EtantDonneBombeZip_QuandImporter_AlorsRefuseAvantDeDecompresser()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize);
            using var writer = entry.Open();
            var zeros = new byte[1024 * 1024];
            for (var i = 0; i < 60; i++)
                writer.Write(zeros);
        }
        stream.Position = 0;

        var result = await Handler().Handle(new ImportProductsCommand(stream, "catalogue.xlsx"), default);

        stream.Length.Should().BeLessThan(ImportLimits.MaxFileBytes);
        result.Errors.Should().ContainSingle().Which.Should().Contain("décompressé");
    }
}
