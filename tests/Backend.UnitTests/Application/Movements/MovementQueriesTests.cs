using FluentAssertions;
using InventorySystem.Application.Common;
using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Dtos;
using InventorySystem.Application.Movements.Queries;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Moq;

namespace InventorySystem.Backend.UnitTests.Application.Movements;

/// <summary>Journal, historique paginé et export des mouvements, y compris pour des références disparues.</summary>
public sealed class MovementQueriesTests
{
    private readonly Mock<IStockMovementRepository> _movements = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly Mock<IExportService> _export = new();

    private readonly Product _produit = Product.Create(Sku.Create("LIV-0400"), "Le Petit Prince", null, 2);
    private readonly Warehouse _boutique = Warehouse.Create("Boutique", null);
    private readonly Warehouse _reserve = Warehouse.Create("Réserve", null);

    private StockMovement Mouvement(MovementType type = MovementType.In, Guid? produit = null, Guid? source = null, Guid? destination = null)
        => StockMovement.Record(produit ?? _produit.Id, source ?? _boutique.Id, type, 3, "Inventaire", destination);

    // --- Journal récent ---

    [Theory]
    [InlineData(0, 1)]
    [InlineData(20, 20)]
    [InlineData(1000, 100)]
    public async Task EtantDonneNombreDeLignesDemande_QuandLireLeJournal_AlorsBorneEntre1Et100(int demande, int attendu)
    {
        _movements.Setup(m => m.ListAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await new GetRecentMovementsQueryHandler(_movements.Object, _products.Object, _warehouses.Object)
            .Handle(new GetRecentMovementsQuery(demande), default);

        _movements.Verify(m => m.ListAllAsync(attendu, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task EtantDonneAucunMouvement_QuandLireLeJournal_AlorsListeVideSansChargerLesLibelles()
    {
        _movements.Setup(m => m.ListAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await new GetRecentMovementsQueryHandler(_movements.Object, _products.Object, _warehouses.Object)
            .Handle(new GetRecentMovementsQuery(), default);

        result.Should().BeEmpty();
        _products.Verify(p => p.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        _warehouses.Verify(w => w.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneTransfertEtProduitDisparu_QuandLireLeJournal_AlorsLibellesResolusOuRemplaces()
    {
        var transfert = Mouvement(MovementType.Transfer, destination: _reserve.Id);
        var orphelin = Mouvement(produit: Guid.NewGuid(), source: Guid.NewGuid());
        _movements.Setup(m => m.ListAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([transfert, orphelin]);
        _products.Setup(p => p.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([_produit]);
        _warehouses.Setup(w => w.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([_boutique, _reserve]);

        var result = await new GetRecentMovementsQueryHandler(_movements.Object, _products.Object, _warehouses.Object)
            .Handle(new GetRecentMovementsQuery(), default);

        result[0].Should().BeEquivalentTo(new
        {
            ProductSku = "LIV-0400", ProductName = "Le Petit Prince", WarehouseName = "Boutique", ToWarehouseName = "Réserve",
            Type = MovementType.Transfer,
        });
        result[1].ProductName.Should().Be("Produit supprimé");
        result[1].ProductSku.Should().Be("—");
        result[1].WarehouseName.Should().Be("—");
        result[1].ToWarehouseName.Should().BeNull();
    }

    // --- Historique d'un produit ---

    [Fact]
    public async Task EtantDonneHistoriqueDUnProduit_QuandLirePage_AlorsPaginationNormaliseeEtDtoComplet()
    {
        var mouvement = Mouvement(MovementType.Out);
        _movements.Setup(m => m.ListByProductPagedAsync(_produit.Id, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(([mouvement], 250));

        var result = await new GetMovementsByProductQueryHandler(_movements.Object)
            .Handle(new GetMovementsByProductQuery(_produit.Id, 0, 999), default);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(100);
        result.TotalCount.Should().Be(250);
        result.Items.Should().ContainSingle().Which.Should().Be(new StockMovementDto(
            mouvement.Id, _produit.Id, _boutique.Id, null, MovementType.Out, 3, "Inventaire", mouvement.CreatedAtUtc));
    }

    // --- Export ---

    private List<MovementExportRow> CapturerExport()
    {
        var lignes = new List<MovementExportRow>();
        _export.Setup(e => e.ExportMovementsAsync(It.IsAny<IReadOnlyList<MovementExportRow>>(), It.IsAny<ExportFormat>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<MovementExportRow>, ExportFormat, CancellationToken>((l, _, _) => lignes.AddRange(l))
            .ReturnsAsync(new ExportFileDto(new MemoryStream(), "text/csv", "mouvements.csv"));
        return lignes;
    }

    private ExportMovementsQueryHandler Exporteur()
        => new(_movements.Object, _products.Object, _warehouses.Object, _export.Object);

    [Fact]
    public async Task EtantDonneProduitPrecise_QuandExporter_AlorsSeulSonHistoriqueEstLu()
    {
        _movements.Setup(m => m.ListByProductAsync(_produit.Id, ExportLimits.MaxRows, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _products.Setup(p => p.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _warehouses.Setup(w => w.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        CapturerExport();

        var file = await Exporteur().Handle(new ExportMovementsQuery(ExportFormat.Csv, _produit.Id), default);

        file.Truncated.Should().BeFalse();
        _movements.Verify(m => m.ListAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneReferencesDisparues_QuandExporter_AlorsLignesLisiblesSansPlanter()
    {
        var transfert = Mouvement(MovementType.Transfer, destination: _reserve.Id);
        var orphelin = Mouvement(produit: Guid.NewGuid(), source: Guid.NewGuid());
        _movements.Setup(m => m.ListAllAsync(ExportLimits.MaxRows, It.IsAny<CancellationToken>())).ReturnsAsync([transfert, orphelin]);
        _products.Setup(p => p.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([_produit]);
        _warehouses.Setup(w => w.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([_boutique, _reserve]);
        var lignes = CapturerExport();

        await Exporteur().Handle(new ExportMovementsQuery(ExportFormat.Csv), default);

        lignes[0].Should().BeEquivalentTo(new { ProductSku = "LIV-0400", WarehouseName = "Boutique", ToWarehouseName = "Réserve" });
        lignes[1].ProductSku.Should().Be("(produit supprimé)");
        lignes[1].WarehouseName.Should().Be("(entrepôt supprimé)");
    }

    [Fact]
    public async Task EtantDonneTransfert_QuandExporter_AlorsSourceEtDestinationResoluesEnUneRequete()
    {
        var transfert = Mouvement(MovementType.Transfer, destination: _reserve.Id);
        _movements.Setup(m => m.ListAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([transfert, Mouvement()]);
        _products.Setup(p => p.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([_produit]);
        IEnumerable<Guid>? demandes = null;
        _warehouses.Setup(w => w.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, CancellationToken>((ids, _) => demandes = ids.ToList())
            .ReturnsAsync([_boutique, _reserve]);
        CapturerExport();

        await Exporteur().Handle(new ExportMovementsQuery(ExportFormat.Excel), default);

        demandes.Should().BeEquivalentTo([_boutique.Id, _reserve.Id], "chaque entrepôt n'est demandé qu'une fois");
        _warehouses.Verify(w => w.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EtantDonneHistoriqueAuPlafond_QuandExporter_AlorsFichierMarqueTronque()
    {
        var mouvements = Enumerable.Range(0, ExportLimits.MaxRows).Select(_ => Mouvement()).ToList();
        _movements.Setup(m => m.ListAllAsync(ExportLimits.MaxRows, It.IsAny<CancellationToken>())).ReturnsAsync(mouvements);
        _products.Setup(p => p.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([_produit]);
        _warehouses.Setup(w => w.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([_boutique]);
        CapturerExport();

        var file = await Exporteur().Handle(new ExportMovementsQuery(ExportFormat.Csv), default);

        file.Truncated.Should().BeTrue();
        file.FileName.Should().Be("mouvements_tronque.csv");
    }
}
