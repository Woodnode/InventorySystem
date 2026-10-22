using FluentAssertions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Commands;
using InventorySystem.Application.Products.Dtos;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;
using InventorySystem.Domain.ValueObjects;
using Moq;

namespace InventorySystem.Backend.UnitTests.Application.Movements;

/// <summary>
/// Cas limites du cas d'usage de mouvement : stock insuffisant (rien n'est écrit), transfert
/// refusé avant de toucher la destination, et alerte de stock bas émise au bon moment seulement.
/// </summary>
public sealed class RecordMovementCasLimitesTests
{
    private readonly Mock<IStockRepository> _stocks = new();
    private readonly Mock<IStockMovementRepository> _movements = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IStockNotifier> _notifier = new();

    private readonly Guid _produit = Guid.NewGuid();
    private readonly Guid _source = Guid.NewGuid();
    private readonly Guid _destination = Guid.NewGuid();

    public RecordMovementCasLimitesTests()
    {
        _movements.Setup(m => m.ExistsByClientGuidAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
    }

    private RecordMovementCommandHandler Creer()
        => new(_stocks.Object, _movements.Object, _products.Object, _unitOfWork.Object, _notifier.Object);

    private Stock StockSource(int quantite)
    {
        var stock = Stock.Create(_produit, _source, quantite);
        _stocks.Setup(s => s.GetOrCreateAsync(_produit, _source, It.IsAny<CancellationToken>())).ReturnsAsync(stock);
        return stock;
    }

    private void TotalAvantSortie(int total)
        => _stocks.Setup(s => s.GetTotalQuantitiesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [_produit] = total });

    private Product Produit(int seuil)
    {
        var produit = Product.Create(Sku.Create("LIV-0001"), "Le Petit Prince", null, seuil);
        _products.Setup(p => p.GetByIdAsync(_produit, It.IsAny<CancellationToken>())).ReturnsAsync(produit);
        return produit;
    }

    private RecordMovementCommand Commande(MovementType type, int quantite, Guid? destination = null, Guid? clientGuid = null)
        => new(_produit, _source, type, quantite, "Test", destination, clientGuid);

    [Fact]
    public async Task EtantDonneStockInsuffisant_QuandSortie_AlorsRefuseeSansRienEnregistrer()
    {
        var stock = StockSource(3);
        TotalAvantSortie(3);

        var act = () => Creer().Handle(Commande(MovementType.Out, 5), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockException>();
        stock.Quantity.Should().Be(3);
        _movements.Verify(m => m.AddAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.NotifyLowStockAsync(It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneStockInsuffisant_QuandTransfert_AlorsDestinationJamaisTouchee()
    {
        StockSource(2);

        var act = () => Creer().Handle(Commande(MovementType.Transfer, 5, _destination), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockException>();
        _stocks.Verify(s => s.GetOrCreateAsync(_produit, _destination, It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneSortieDeToutLeStock_QuandEnregistrer_AlorsStockAZero()
    {
        var stock = StockSource(7);
        TotalAvantSortie(7);
        Produit(seuil: 0);

        await Creer().Handle(Commande(MovementType.Out, 7), CancellationToken.None);

        stock.Quantity.Should().Be(0);
    }

    [Fact]
    public async Task EtantDonneSortieQuiAtteintExactementLeSeuil_QuandEnregistrer_AlorsAlerteEmiseAvecLesBonnesValeurs()
    {
        // Le seuil est inclusif : à 2 unités pour un seuil de 2, le produit est « bas ».
        StockSource(5);
        TotalAvantSortie(5);
        var produit = Produit(seuil: 2);
        LowStockNotification? alerte = null;
        _notifier.Setup(n => n.NotifyLowStockAsync(It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()))
            .Callback<LowStockNotification, CancellationToken>((n, _) => alerte = n);

        await Creer().Handle(Commande(MovementType.Out, 3), CancellationToken.None);

        alerte.Should().NotBeNull();
        alerte!.ProductId.Should().Be(produit.Id);
        alerte.Sku.Should().Be("LIV-0001");
        alerte.Name.Should().Be("Le Petit Prince");
        alerte.Quantity.Should().Be(2);
        alerte.LowStockThreshold.Should().Be(2);
    }

    [Fact]
    public async Task EtantDonneSortieQuiResteAuDessusDuSeuil_QuandEnregistrer_AlorsAucuneAlerte()
    {
        StockSource(10);
        TotalAvantSortie(10);
        Produit(seuil: 2);

        await Creer().Handle(Commande(MovementType.Out, 1), CancellationToken.None);

        _notifier.Verify(n => n.NotifyLowStockAsync(It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneStockReparti_QuandSortieDansUnSeulEntrepot_AlorsSeuilCompareAuTotalTousEntrepots()
    {
        // 4 unités ici, mais 20 au total : la sortie de 3 ne rend pas le produit « bas ».
        StockSource(4);
        TotalAvantSortie(20);
        Produit(seuil: 5);

        await Creer().Handle(Commande(MovementType.Out, 3), CancellationToken.None);

        _notifier.Verify(n => n.NotifyLowStockAsync(It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneProduitSupprimeEntreTemps_QuandSortieSousLeSeuil_AlorsMouvementEnregistreSansAlerte()
    {
        StockSource(5);
        TotalAvantSortie(5);
        _products.Setup(p => p.GetByIdAsync(_produit, It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        await Creer().Handle(Commande(MovementType.Out, 5), CancellationToken.None);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.NotifyLowStockAsync(It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(MovementType.In)]
    [InlineData(MovementType.Transfer)]
    public async Task EtantDonneEntreeOuTransfert_QuandEnregistrer_AlorsNiTotalCalculeNiAlerte(MovementType type)
    {
        // Ni une entrée ni un transfert ne font baisser le stock total.
        StockSource(1);
        _stocks.Setup(s => s.GetOrCreateAsync(_produit, _destination, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stock.Create(_produit, _destination));

        await Creer().Handle(
            Commande(type, 1, type == MovementType.Transfer ? _destination : null), CancellationToken.None);

        _stocks.Verify(s => s.GetTotalQuantitiesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.NotifyLowStockAsync(It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneMouvementWebSansClientGuid_QuandEnregistrer_AlorsIdentifiantGenereEtRenvoye()
    {
        StockSource(0);
        StockMovement? enregistre = null;
        _movements.Setup(m => m.AddAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()))
            .Callback<StockMovement, CancellationToken>((m, _) => enregistre = m);

        var id = await Creer().Handle(Commande(MovementType.In, 4), CancellationToken.None);

        enregistre.Should().NotBeNull();
        id.Should().Be(enregistre!.Id);
        enregistre.ClientGuid.Should().NotBeEmpty();
        _movements.Verify(m => m.ExistsByClientGuidAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneMouvementMobile_QuandEnregistrer_AlorsClientGuidConserve()
    {
        StockSource(0);
        var clientGuid = Guid.NewGuid();
        StockMovement? enregistre = null;
        _movements.Setup(m => m.AddAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()))
            .Callback<StockMovement, CancellationToken>((m, _) => enregistre = m);

        await Creer().Handle(Commande(MovementType.In, 4, clientGuid: clientGuid), CancellationToken.None);

        enregistre!.ClientGuid.Should().Be(clientGuid);
        enregistre.Type.Should().Be(MovementType.In);
        enregistre.Quantity.Should().Be(4);
        enregistre.Reason.Should().Be("Test");
    }

    [Fact]
    public async Task EtantDonneTransfert_QuandEnregistrer_AlorsUnSeulMouvementAvecSourceEtDestination()
    {
        StockSource(10);
        var destination = Stock.Create(_produit, _destination, 1);
        _stocks.Setup(s => s.GetOrCreateAsync(_produit, _destination, It.IsAny<CancellationToken>())).ReturnsAsync(destination);
        var enregistres = new List<StockMovement>();
        _movements.Setup(m => m.AddAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()))
            .Callback<StockMovement, CancellationToken>((m, _) => enregistres.Add(m));

        await Creer().Handle(Commande(MovementType.Transfer, 4, _destination), CancellationToken.None);

        enregistres.Should().ContainSingle();
        enregistres[0].WarehouseId.Should().Be(_source);
        enregistres[0].ToWarehouseId.Should().Be(_destination);
        destination.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task EtantDonneEchecDeLEnregistrement_QuandSortieSousLeSeuil_AlorsAucuneAlerte()
    {
        // L'alerte suit l'écriture : si la base refuse, personne n'est prévenu d'un stock fictif.
        StockSource(5);
        TotalAvantSortie(5);
        Produit(seuil: 3);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Base indisponible"));

        var act = () => Creer().Handle(Commande(MovementType.Out, 4), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _notifier.Verify(n => n.NotifyLowStockAsync(It.IsAny<LowStockNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
