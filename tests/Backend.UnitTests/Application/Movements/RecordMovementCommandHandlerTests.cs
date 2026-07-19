using FluentAssertions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Commands;
using InventorySystem.Application.Products.Dtos;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;
using InventorySystem.Domain.ValueObjects;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Movements;

/// <summary>
/// RecordMovementCommandHandler est le cas d'usage le plus critique du domaine (idempotence
/// mobile, atomicité des transferts, notification de franchissement de seuil) — voir plan
/// §8.3/§8.4. Testé ici en isolation (Moq), sans base de données.
/// </summary>
public sealed class RecordMovementCommandHandlerTests
{
    private readonly Mock<IStockRepository> _stocks = new();
    private readonly Mock<IStockMovementRepository> _movements = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IStockNotifier> _notifier = new();

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _otherWarehouseId = Guid.NewGuid();

    private RecordMovementCommandHandler CreateHandler()
        => new(_stocks.Object, _movements.Object, _products.Object, _unitOfWork.Object, _notifier.Object);

    private static Product NewProduct(int lowStockThreshold)
        => Product.Create(Sku.Create("SKU-TEST"), "Produit test", null, lowStockThreshold);

    public RecordMovementCommandHandlerTests()
    {
        // Par défaut, aucun mouvement déjà synchronisé (voir tests d'idempotence dédiés).
        _movements
            .Setup(m => m.ExistsByClientGuidAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task Handle_WithAlreadySyncedClientGuid_ThrowsAndDoesNotPersist()
    {
        var clientGuid = Guid.NewGuid();
        _movements
            .Setup(m => m.ExistsByClientGuidAsync(clientGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new RecordMovementCommand(
            _productId, _warehouseId, MovementType.In, 5, null, null, clientGuid);

        var act = async () => await CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidMovementException>();
        _stocks.Verify(s => s.GetOrCreateAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InMovement_IncreasesStockAndSaves()
    {
        var stock = Stock.Create(_productId, _warehouseId, 10);
        _stocks
            .Setup(s => s.GetOrCreateAsync(_productId, _warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        var command = new RecordMovementCommand(
            _productId, _warehouseId, MovementType.In, 5, "Réception", null, Guid.NewGuid());

        await CreateHandler().Handle(command, CancellationToken.None);

        stock.Quantity.Should().Be(15);
        _stocks.Verify(s => s.Update(stock), Times.Once);
        _movements.Verify(m => m.AddAsync(
            It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.NotifyLowStockAsync(
            It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OutMovement_DecreasesStockAndSaves()
    {
        var stock = Stock.Create(_productId, _warehouseId, 10);
        _stocks
            .Setup(s => s.GetOrCreateAsync(_productId, _warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);
        _stocks
            .Setup(s => s.GetTotalQuantitiesAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(_productId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [_productId] = 10 });
        _products
            .Setup(p => p.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewProduct(lowStockThreshold: 0));

        var command = new RecordMovementCommand(
            _productId, _warehouseId, MovementType.Out, 4, "Vente", null, Guid.NewGuid());

        await CreateHandler().Handle(command, CancellationToken.None);

        stock.Quantity.Should().Be(6);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TransferMovement_MovesQuantityBetweenWarehousesAndSavesOnce()
    {
        var source = Stock.Create(_productId, _warehouseId, 10);
        var destination = Stock.Create(_productId, _otherWarehouseId, 2);
        _stocks
            .Setup(s => s.GetOrCreateAsync(_productId, _warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);
        _stocks
            .Setup(s => s.GetOrCreateAsync(_productId, _otherWarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);

        var command = new RecordMovementCommand(
            _productId, _warehouseId, MovementType.Transfer, 3, null, _otherWarehouseId, Guid.NewGuid());

        await CreateHandler().Handle(command, CancellationToken.None);

        source.Quantity.Should().Be(7);
        destination.Quantity.Should().Be(5);
        _stocks.Verify(s => s.Update(source), Times.Once);
        _stocks.Verify(s => s.Update(destination), Times.Once);
        // Une seule transaction pour les deux mutations de stock + le mouvement (plan §3 : atomicité).
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        // Un transfert redistribue sans changer la quantité totale — jamais de notification de stock bas.
        _notifier.Verify(n => n.NotifyLowStockAsync(
            It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OutMovementCrossingThreshold_NotifiesLowStockOnce()
    {
        // Seuil à 5 : 10 -> 4 fait bien franchir le seuil (pas bas avant, bas après).
        var stock = Stock.Create(_productId, _warehouseId, 10);
        _stocks
            .Setup(s => s.GetOrCreateAsync(_productId, _warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);
        _stocks
            .Setup(s => s.GetTotalQuantitiesAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [_productId] = 10 });
        var product = NewProduct(lowStockThreshold: 5);
        _products
            .Setup(p => p.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = new RecordMovementCommand(
            _productId, _warehouseId, MovementType.Out, 6, null, null, Guid.NewGuid());

        await CreateHandler().Handle(command, CancellationToken.None);

        // product.Id (pas _productId) : c'est le Product retourné par le mock qui porte
        // l'identité utilisée par le handler pour construire la notification.
        _notifier.Verify(n => n.NotifyLowStockAsync(
            It.Is<LowStockNotification>(x => x.ProductId == product.Id && x.Quantity == 4),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OutMovementAlreadyBelowThreshold_DoesNotNotifyAgain()
    {
        // Seuil à 5 : déjà à 4 avant la sortie -> pas de nouveau franchissement, pas de spam.
        var stock = Stock.Create(_productId, _warehouseId, 4);
        _stocks
            .Setup(s => s.GetOrCreateAsync(_productId, _warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);
        _stocks
            .Setup(s => s.GetTotalQuantitiesAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [_productId] = 4 });
        _products
            .Setup(p => p.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewProduct(lowStockThreshold: 5));

        var command = new RecordMovementCommand(
            _productId, _warehouseId, MovementType.Out, 1, null, null, Guid.NewGuid());

        await CreateHandler().Handle(command, CancellationToken.None);

        _notifier.Verify(n => n.NotifyLowStockAsync(
            It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
