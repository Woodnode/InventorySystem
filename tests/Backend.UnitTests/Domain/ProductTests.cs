using FluentAssertions;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;
using InventorySystem.Domain.ValueObjects;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Domain;

/// <summary>
/// Product est un catalogue pur depuis le refactor multi-entrepôt (plan §3) : la quantité
/// vit dans Stock (voir StockTests). Ces tests couvrent la création et le calcul de
/// "stock bas" à partir d'un total fourni par la couche Application (agrégation Stock).
/// </summary>
public sealed class ProductTests
{
    private static Product NewProduct(int threshold = 3)
        => Product.Create(Sku.Create("ABC-123"), "Vis M6", null, threshold);

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        var act = () => Product.Create(Sku.Create("ABC-123"), "", null, 3);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithNegativeThreshold_Throws()
    {
        var act = () => Product.Create(Sku.Create("ABC-123"), "Vis M6", null, -1);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void IsLowOnStock_WhenTotalAtOrBelowThreshold_ReturnsTrue()
    {
        var product = NewProduct(threshold: 3);

        product.IsLowOnStock(totalQuantity: 3).Should().BeTrue();
        product.IsLowOnStock(totalQuantity: 2).Should().BeTrue();
    }

    [Fact]
    public void IsLowOnStock_WhenTotalAboveThreshold_ReturnsFalse()
    {
        var product = NewProduct(threshold: 3);

        product.IsLowOnStock(totalQuantity: 4).Should().BeFalse();
    }

    [Fact]
    public void Sku_WhenTooShort_Throws()
    {
        var act = () => Sku.Create("AB");

        act.Should().Throw<DomainException>();
    }
}
