using System.Net;
using FluentAssertions;
using InventorySystem.Application.Dashboard.Dtos;

namespace InventorySystem.Backend.IntegrationTests.Inventory;

/// <summary>Tableau de bord (/dashboard/stats) agrégé en base, et sonde de santé.</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class TableauDeBordEtSanteTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public TableauDeBordEtSanteTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task EtantDonneEmploye_QuandLireLeTableauDeBord_AlorsPeriodeParDefautDeTrenteJours()
    {
        var stats = await _client.LireAsync<DashboardStatsDto>("/api/v1/dashboard/stats", await RoleTokens.GetAsync(_factory, RoleTokens.Employe));

        stats.PeriodDays.Should().Be(30);
        stats.DailyFlow.Should().HaveCount(30);
        stats.TotalProducts.Should().BeGreaterThan(0, "le jeu de démonstration est chargé en développement");
    }

    [Theory]
    [InlineData(1, 7)]
    [InlineData(90, 90)]
    [InlineData(5000, 180)]
    public async Task EtantDonnePeriodeDemandee_QuandLireLeTableauDeBord_AlorsBorneeEntre7Et180Jours(int demande, int attendu)
    {
        var stats = await _client.LireAsync<DashboardStatsDto>($"/api/v1/dashboard/stats?periodDays={demande}",
            await RoleTokens.GetAsync(_factory, RoleTokens.Employe));

        stats.PeriodDays.Should().Be(attendu);
        stats.DailyFlow.Should().HaveCount(attendu);
    }

    [Fact]
    public async Task EtantDonneEntreeEtSortieDuJour_QuandLireLeTableauDeBord_AlorsComptesDansLeSoldeDuJour()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);
        DashboardStatsDto avant, apres;
        var essais = 0;
        do
        {
            // Rejoué si minuit UTC passe entre les deux lectures : la fenêtre aurait glissé d'un jour.
            var entrepot = await _client.CreerEntrepotAsync(token);
            var produit = await _client.CreerProduitAsync(token, entrepot, 0);
            avant = await _client.LireAsync<DashboardStatsDto>("/api/v1/dashboard/stats?periodDays=7", token);

            await _client.EnregistrerMouvementAsync(token, produit, entrepot, "In", 40);
            await _client.EnregistrerMouvementAsync(token, produit, entrepot, "Out", 15);
            apres = await _client.LireAsync<DashboardStatsDto>("/api/v1/dashboard/stats?periodDays=7", token);
        }
        while (apres.DailyFlow[^1].Date != avant.DailyFlow[^1].Date && ++essais < 2);

        apres.DailyFlow[^1].Date.Should().Be(avant.DailyFlow[^1].Date);
        (apres.DailyFlow[^1].In - avant.DailyFlow[^1].In).Should().Be(40);
        (apres.DailyFlow[^1].Out - avant.DailyFlow[^1].Out).Should().Be(15);
        (apres.NetCurrentPeriod - avant.NetCurrentPeriod).Should().Be(25);
        (apres.TotalProducts - avant.TotalProducts).Should().Be(0);
    }

    [Fact]
    public async Task EtantDonneTransfert_QuandLireLeTableauDeBord_AlorsSoldeInchange()
    {
        // Un transfert déplace du stock sans en créer ni en détruire.
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);
        var source = await _client.CreerEntrepotAsync(token);
        var destination = await _client.CreerEntrepotAsync(token);
        var produit = await _client.CreerProduitAsync(token, source, 10);
        var avant = await _client.LireAsync<DashboardStatsDto>("/api/v1/dashboard/stats?periodDays=7", token);

        await _client.EnregistrerMouvementAsync(token, produit, source, "Transfer", 4, destination);
        var apres = await _client.LireAsync<DashboardStatsDto>("/api/v1/dashboard/stats?periodDays=7", token);

        apres.NetCurrentPeriod.Should().Be(avant.NetCurrentPeriod);
        apres.WarehouseLoads.Single(w => w.WarehouseId == destination).TotalQuantity.Should().Be(4);
    }

    [Fact]
    public async Task EtantDonneNouvelEntrepotDesactive_QuandLireLeTableauDeBord_AlorsCompteParmiLesInactifs()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);
        var avant = await _client.LireAsync<DashboardStatsDto>("/api/v1/dashboard/stats", token);
        var id = await _client.CreerEntrepotAsync(token);

        await _client.SendAsync(HttpMethod.Patch, $"/api/v1/warehouses/{id}/active", token,
            System.Net.Http.Json.JsonContent.Create(new { isActive = false }));
        var apres = await _client.LireAsync<DashboardStatsDto>("/api/v1/dashboard/stats", token);

        (apres.InactiveWarehouses - avant.InactiveWarehouses).Should().Be(1);
        apres.ActiveWarehouses.Should().Be(avant.ActiveWarehouses);
    }

    [Fact]
    public async Task EtantDonneBaseDisponible_QuandSonderLaSante_AlorsSainSansAuthentification()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy", "la sonde ne détaille rien de l'infrastructure");
    }
}
