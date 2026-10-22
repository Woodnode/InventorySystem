using FluentAssertions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Dashboard.Dtos;
using InventorySystem.Application.Dashboard.Queries;
using InventorySystem.Domain.Entities;
using Moq;

namespace InventorySystem.Backend.UnitTests.Application.Dashboard;

/// <summary>
/// Agrégats du tableau de bord hors calendrier : entrepôts actifs, charges, collections, et
/// cohérence de la fenêtre demandée. Aucune assertion ne dépend de la date du jour : les
/// bornes attendues sont relues dans la réponse elle-même.
/// </summary>
public sealed class TableauDeBordAgregatsTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly Mock<IStockMovementRepository> _movements = new();
    private readonly Mock<IStockRepository> _stocks = new();

    public TableauDeBordAgregatsTests()
    {
        _products.Setup(p => p.GetCatalogTotalsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CatalogTotals(0, 0m));
        _products.Setup(p => p.CountByCollectionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _warehouses.Setup(w => w.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _movements.Setup(m => m.SumByDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _stocks.Setup(s => s.GetTotalsByWarehouseAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private Task<DashboardStatsDto> CalculerAsync(int periode = 30)
        => new GetDashboardStatsQueryHandler(_products.Object, _warehouses.Object, _movements.Object, _stocks.Object,
                new HorlogeFixe(new DateTimeOffset(2026, 10, 22, 0, 0, 1, TimeSpan.Zero)))
            .Handle(new GetDashboardStatsQuery(periode), default);

    [Fact]
    public async Task EtantDonneEntrepotsActifsEtInactifs_QuandCalculer_AlorsComptesSepares()
    {
        var ferme = Warehouse.Create("Ancien dépôt", null);
        ferme.Deactivate();
        _warehouses.Setup(w => w.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Warehouse.Create("Boutique", null), Warehouse.Create("Réserve", null), ferme]);

        var result = await CalculerAsync();

        result.ActiveWarehouses.Should().Be(2);
        result.InactiveWarehouses.Should().Be(1);
    }

    [Fact]
    public async Task EtantDonneChargesParEntrepot_QuandCalculer_AlorsRecopieesTellesQuelles()
    {
        var id = Guid.NewGuid();
        _stocks.Setup(s => s.GetTotalsByWarehouseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WarehouseLoad(id, "Boutique", 14, 312)]);

        var result = await CalculerAsync();

        result.WarehouseLoads.Should().ContainSingle().Which.Should().Be(new WarehouseLoadDto(id, "Boutique", 14, 312));
    }

    [Fact]
    public async Task EtantDonneCollections_QuandCalculer_AlorsSixPlusFourniesDemandees()
    {
        _products.Setup(p => p.CountByCollectionAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CollectionCount("Folio", 9), new CollectionCount("Pléiade", 4)]);

        var result = await CalculerAsync();

        result.TopCollections.Select(c => c.Collection).Should().Equal("Folio", "Pléiade");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(180)]
    public async Task EtantDonnePeriode_QuandCalculer_AlorsJoursContigusEtHistoriqueLuDepuisLaPeriodePrecedente(int periode)
    {
        DateTime? depuis = null;
        _movements.Setup(m => m.SumByDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((d, _) => depuis = d)
            .ReturnsAsync([]);

        var result = await CalculerAsync(periode);

        var jours = result.DailyFlow.Select(f => f.Date).ToList();
        jours.Zip(jours.Skip(1), (a, b) => b.DayNumber - a.DayNumber).Should().OnlyContain(ecart => ecart == 1);
        DateOnly.FromDateTime(depuis!.Value).Should().Be(jours[0].AddDays(-periode),
            "une seule lecture couvre la période affichée et la précédente, de même longueur");
        depuis.Value.TimeOfDay.Should().Be(TimeSpan.Zero, "les journées sont pleines, pas coupées à l'heure de l'appel");
    }

    [Fact]
    public async Task EtantDonneMouvementsHorsFenetre_QuandCalculer_AlorsIgnoresDansLesDeuxPeriodes()
    {
        // Le dépôt peut renvoyer plus large que demandé : seuls les jours des deux périodes comptent.
        var reference = await CalculerAsync(7);
        var bienAvant = reference.DailyFlow[0].Date.AddDays(-30).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        _movements.Setup(m => m.SumByDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DailyMovementTotals(bienAvant, 500, 0)]);

        var result = await CalculerAsync(7);

        result.NetCurrentPeriod.Should().Be(0);
        result.NetPreviousPeriod.Should().Be(0);
        result.ChangePercent.Should().BeNull();
    }

    [Fact]
    public async Task EtantDonneSortiesSuperieuresAuxEntrees_QuandCalculer_AlorsSoldeNegatifEtVariationSignee()
    {
        var reference = await CalculerAsync(7);
        var premierJour = reference.DailyFlow[0].Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        _movements.Setup(m => m.SumByDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DailyMovementTotals(premierJour, 10, 40),            // période courante : -30
                new DailyMovementTotals(premierJour.AddDays(-3), 0, 10),  // période précédente : -10
            ]);

        var result = await CalculerAsync(7);

        result.NetCurrentPeriod.Should().Be(-30);
        result.NetPreviousPeriod.Should().Be(-10);
        result.ChangePercent.Should().Be(-200, "(-30 - -10) / |-10|");
        result.DailyFlow[0].Should().Be(new DailyFlowDto(reference.DailyFlow[0].Date, 10, 40, -30));
    }
}
