using System.Text;
using FluentAssertions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Commands;
using InventorySystem.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Products;

/// <summary>
/// Non-régression de l'audit de sécurité sur l'import : plafond de lignes, troncature de la
/// Location (nom d'entrepôt) et absence de message interne de bibliothèque renvoyé au client.
/// </summary>
public sealed class ImportProductsCommandHandlerTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStockRepository> _stocks = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public ImportProductsCommandHandlerTests()
    {
        _warehouses
            .Setup(w => w.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Warehouse>());
        _stocks
            .Setup(s => s.GetOrCreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid productId, Guid warehouseId, CancellationToken _) => Stock.Create(productId, warehouseId));
    }

    private ImportProductsCommandHandler CreateHandler()
        => new(_products.Object, _stocks.Object, _warehouses.Object, _unitOfWork.Object,
            NullLogger<ImportProductsCommandHandler>.Instance);

    private static MemoryStream Csv(string content) => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Handle_CsvAboveRowLimit_RejectsWithoutPersisting()
    {
        var builder = new StringBuilder("Code produit;Titre\n");
        for (var i = 0; i <= ImportProductsCommandHandler.MaxImportRows; i++)
            builder.Append("SKU-").Append(i).Append(";Titre\n");

        var result = await CreateHandler().Handle(
            new ImportProductsCommand(Csv(builder.ToString()), "import.csv"), CancellationToken.None);

        result.ProductsCreated.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().Contain("limite");
        _products.Verify(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CsvWithOverlongLocation_TruncatesWarehouseName()
    {
        Warehouse? created = null;
        _warehouses
            .Setup(w => w.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()))
            .Callback((Warehouse w, CancellationToken _) => created = w)
            .Returns(Task.CompletedTask);

        var csv = "Code produit;Titre;Location\nSKU-1;Titre;" + new string('A', 5_000) + "\n";

        await CreateHandler().Handle(new ImportProductsCommand(Csv(csv), "import.csv"), CancellationToken.None);

        created.Should().NotBeNull();
        created!.Name.Length.Should().Be(200);
    }

    [Fact]
    public async Task Handle_UnreadableExcel_ReturnsGenericMessage()
    {
        // Un faux classeur fait lever une exception à la bibliothèque : son message (chemins,
        // détails internes) ne doit pas remonter tel quel jusqu'au client.
        var garbage = new MemoryStream(Encoding.UTF8.GetBytes("ceci n'est pas un classeur"));

        var result = await CreateHandler().Handle(
            new ImportProductsCommand(garbage, "import.xlsx"), CancellationToken.None);

        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Le fichier Excel n'a pas pu être lu. Vérifiez son format.");
    }
}
