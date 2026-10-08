using FluentAssertions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Dashboard.Queries;
using InventorySystem.Domain.Entities;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Dashboard;

/// <summary>
/// Couvre la logique d'agregation du tableau de bord : trous de calendrier combles,
/// fenetre glissante decoupee en deux periodes, et variation non calculee quand la
/// reference est nulle.
/// </summary>
public sealed class GetDashboardStatsQueryHandlerTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly Mock<IStockMovementRepository> _movements = new();
    private readonly Mock<IStockRepository> _stocks = new();

    private GetDashboardStatsQueryHandler CreateHandler()
        => new(_products.Object, _warehouses.Object, _movements.Object, _stocks.Object);

    public GetDashboardStatsQueryHandlerTests()
    {
        _products
            .Setup(p => p.GetCatalogTotalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogTotals(42, 1234.567m));
        _products
            .Setup(p => p.CountByCollectionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CollectionCount("Cartographie", 7) });
        _warehouses
            .Setup(w => w.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Warehouse>());
        _movements
            .Setup(m => m.SumByDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DailyMovementTotals>());
        _stocks
            .Setup(s => s.GetTotalsByWarehouseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WarehouseLoad>());
    }

    [Fact]
    public async Task Handle_EmitsOneEntryPerDay_IncludingDaysWithoutMovement()
    {
        // Un seul jour porte des mouvements : les autres doivent tout de meme apparaitre
        // a zero, sinon la courbe se contracte et donne a lire un rythme inexistant.
        var hier = DateTime.UtcNow.Date.AddDays(-1);
        _movements
            .Setup(m => m.SumByDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new DailyMovementTotals(hier, 100, 40) });

        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(7), CancellationToken.None);

        result.DailyFlow.Should().HaveCount(7);
        result.DailyFlow.Select(f => f.Date).Should().BeInAscendingOrder();
        result.DailyFlow.Should().ContainSingle(f => f.Net != 0)
            .Which.Should().BeEquivalentTo(new { In = 100, Out = 40, Net = 60 });
        result.NetCurrentPeriod.Should().Be(60);
    }

    [Fact]
    public async Task Handle_SplitsWindowIntoCurrentAndPreviousPeriods()
    {
        var aujourdhui = DateTime.UtcNow.Date;
        _movements
            .Setup(m => m.SumByDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DailyMovementTotals(aujourdhui, 50, 0),              // periode courante
                new DailyMovementTotals(aujourdhui.AddDays(-10), 20, 0), // periode precedente
            });

        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(7), CancellationToken.None);

        result.NetCurrentPeriod.Should().Be(50);
        result.NetPreviousPeriod.Should().Be(20);
        result.ChangePercent.Should().Be(150); // (50 - 20) / 20
    }

    [Fact]
    public async Task Handle_WithNoPreviousActivity_LeavesChangePercentNull()
    {
        // « +100 % » a partir de zero serait une information inventee.
        _movements
            .Setup(m => m.SumByDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new DailyMovementTotals(DateTime.UtcNow.Date, 30, 0) });

        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(7), CancellationToken.None);

        result.NetPreviousPeriod.Should().Be(0);
        result.ChangePercent.Should().BeNull();
    }

    [Theory]
    [InlineData(1, 7)]      // sous le plancher
    [InlineData(365, 180)]  // au-dessus du plafond
    [InlineData(30, 30)]
    public async Task Handle_ClampsPeriodToSupportedRange(int demande, int attendu)
    {
        // Une fenetre non bornee ferait parcourir tout l'historique a chaque appel.
        var result = await CreateHandler().Handle(
            new GetDashboardStatsQuery(demande), CancellationToken.None);

        result.PeriodDays.Should().Be(attendu);
        result.DailyFlow.Should().HaveCount(attendu);
    }

    [Fact]
    public async Task Handle_ReportsCatalogTotalsFromRepository()
    {
        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), CancellationToken.None);

        result.TotalProducts.Should().Be(42);
        result.TotalWeightGrams.Should().Be(1234.57m); // arrondi a deux decimales
        result.TopCollections.Should().ContainSingle()
            .Which.Collection.Should().Be("Cartographie");
    }
}
