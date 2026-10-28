using FluentAssertions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Commands;
using InventorySystem.Domain.Entities;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Movements;

/// <summary>
/// Non-régression de l'audit de sécurité : borne haute sur la quantité d'un mouvement et
/// refus des entrepôts désactivés (source comme destination).
/// </summary>
public sealed class RecordMovementCommandValidatorTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _activeWarehouseId = Guid.NewGuid();
    private readonly Guid _inactiveWarehouseId = Guid.NewGuid();

    public RecordMovementCommandValidatorTests()
    {
        _products
            .Setup(p => p.ExistsAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _warehouses
            .Setup(w => w.IsActiveAsync(_activeWarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _warehouses
            .Setup(w => w.IsActiveAsync(_inactiveWarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private RecordMovementCommandValidator CreateValidator() => new(_products.Object, _warehouses.Object);

    [Fact]
    public async Task Validate_WithActiveWarehouseAndReasonableQuantity_IsValid()
    {
        var command = new RecordMovementCommand(
            _productId, _activeWarehouseId, MovementType.In, 5, null, null, null);

        var result = await CreateValidator().ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithQuantityAboveBusinessLimit_IsInvalid()
    {
        var command = new RecordMovementCommand(
            _productId, _activeWarehouseId, MovementType.In,
            RecordMovementCommandValidator.MaxQuantityPerMovement + 1, null, null, null);

        var result = await CreateValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RecordMovementCommand.Quantity));
    }

    [Fact]
    public async Task Validate_WithInactiveSourceWarehouse_IsInvalid()
    {
        var command = new RecordMovementCommand(
            _productId, _inactiveWarehouseId, MovementType.In, 5, null, null, null);

        var result = await CreateValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RecordMovementCommand.WarehouseId));
    }

    [Fact]
    public async Task Validate_TransferToInactiveWarehouse_IsInvalid()
    {
        var command = new RecordMovementCommand(
            _productId, _activeWarehouseId, MovementType.Transfer, 5, null, _inactiveWarehouseId, null);

        var result = await CreateValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RecordMovementCommand.ToWarehouseId));
    }
}
