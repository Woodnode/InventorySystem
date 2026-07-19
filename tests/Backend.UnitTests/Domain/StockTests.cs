using FluentAssertions;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Domain;

/// <summary>
/// Règles de quantité par entrepôt (plan §3 : refactor multi-entrepôt).
/// Anciennement portées par Product.AddStock/RemoveStock — voir git history si besoin.
/// </summary>
public sealed class StockTests
{
    private static Stock NewStock(int quantity = 10)
        => Stock.Create(Guid.NewGuid(), Guid.NewGuid(), quantity);

    [Fact]
    public void Create_WithNegativeInitialQuantity_Throws()
    {
        var act = () => Stock.Create(Guid.NewGuid(), Guid.NewGuid(), -1);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Decrease_WhenAmountExceedsQuantity_Throws()
    {
        var stock = NewStock(quantity: 5);

        var act = () => stock.Decrease(6);

        act.Should().Throw<InsufficientStockException>();
    }

    [Fact]
    public void Decrease_WithinAvailableQuantity_Succeeds()
    {
        var stock = NewStock(quantity: 5);

        stock.Decrease(2);

        stock.Quantity.Should().Be(3);
    }

    [Fact]
    public void Increase_AddsToQuantity()
    {
        var stock = NewStock(quantity: 10);

        stock.Increase(5);

        stock.Quantity.Should().Be(15);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Increase_WithNonPositiveAmount_Throws(int amount)
    {
        var stock = NewStock();

        var act = () => stock.Increase(amount);

        act.Should().Throw<InvalidMovementException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Decrease_WithNonPositiveAmount_Throws(int amount)
    {
        var stock = NewStock();

        var act = () => stock.Decrease(amount);

        act.Should().Throw<InvalidMovementException>();
    }
}
