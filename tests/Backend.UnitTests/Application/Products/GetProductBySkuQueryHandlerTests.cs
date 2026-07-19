using FluentAssertions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Queries;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Products;

/// <summary>
/// GetProductBySkuQuery alimente le scan mobile (le code scanné == le SKU du produit) —
/// voir plan §8.1. Testé ici en isolation (Moq), sans base de données.
/// </summary>
public sealed class GetProductBySkuQueryHandlerTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStockRepository> _stocks = new();

    private GetProductBySkuQueryHandler CreateHandler()
        => new(_products.Object, _stocks.Object);

    [Fact]
    public async Task Handle_ExistingSku_ReturnsProductDto()
    {
        var product = Product.Create(Sku.Create("SKU-TEST"), "Produit test", null, lowStockThreshold: 5);
        _products
            .Setup(p => p.GetBySkuAsync("SKU-TEST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _stocks
            .Setup(s => s.GetTotalQuantitiesAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(product.Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [product.Id] = 12 });

        var result = await CreateHandler().Handle(new GetProductBySkuQuery("SKU-TEST"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Sku.Should().Be("SKU-TEST");
        result.Quantity.Should().Be(12);
    }

    [Fact]
    public async Task Handle_UnknownSku_ReturnsNull()
    {
        _products
            .Setup(p => p.GetBySkuAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await CreateHandler().Handle(new GetProductBySkuQuery("UNKNOWN"), CancellationToken.None);

        result.Should().BeNull();
        _stocks.Verify(s => s.GetTotalQuantitiesAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
