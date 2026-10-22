using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Movements.Dtos;
using InventorySystem.Application.Stocks.Dtos;
using InventorySystem.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Backend.IntegrationTests.Inventory;

/// <summary>
/// Routes des mouvements (/movements) : enregistrement par un Employé, idempotence de la
/// synchronisation mobile, journal récent, historique paginé d'un produit et export.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class MouvementsEndpointsTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public MouvementsEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Gestionnaire, string Employe, Guid Entrepot, Guid Produit)> PreparerAsync(int quantite = 10)
    {
        var gestionnaire = await RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);
        var employe = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);
        var entrepot = await _client.CreerEntrepotAsync(gestionnaire);
        var produit = await _client.CreerProduitAsync(gestionnaire, entrepot, quantite);
        return (gestionnaire, employe, entrepot, produit);
    }

    [Fact]
    public async Task EtantDonneEmploye_QuandEnregistrerUneSortie_AlorsCreeAvecLienVersLHistorique()
    {
        var (_, employe, entrepot, produit) = await PreparerAsync();

        var response = await _client.EnregistrerMouvementAsync(employe, produit, entrepot, "Out", 4);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().EndWithEquivalentOf($"/api/v1/Movements/product/{produit}");
        (await _client.LireAsync<List<StockDto>>($"/api/v1/stocks/product/{produit}", employe)).Single().Quantity.Should().Be(6);
    }

    [Fact]
    public async Task EtantDonneMemeClientGuidRejoue_QuandSynchroniser_AlorsSecondEnvoiRefuseEtStockCompteUneFois()
    {
        // Le mobile renvoie un mouvement dont il n'a pas reçu la confirmation : il ne doit pas compter double.
        var (_, employe, entrepot, produit) = await PreparerAsync();
        var clientGuid = Guid.NewGuid();

        var premier = await _client.EnregistrerMouvementAsync(employe, produit, entrepot, "In", 5, clientGuid: clientGuid);
        var rejeu = await _client.EnregistrerMouvementAsync(employe, produit, entrepot, "In", 5, clientGuid: clientGuid);

        premier.StatusCode.Should().Be(HttpStatusCode.Created);
        rejeu.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejeu.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Be("Ce mouvement a déjà été synchronisé.");
        (await _client.LireAsync<List<StockDto>>($"/api/v1/stocks/product/{produit}", employe)).Single().Quantity.Should().Be(15);
    }

    [Fact]
    public async Task EtantDonneTypeNumerique_QuandEnregistrer_AlorsAccepteCommeLeTexte()
    {
        // Les anciens clients envoyaient 1/2/3 : le convertisseur d'énumérations accepte encore les nombres.
        var (_, employe, entrepot, produit) = await PreparerAsync();

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/movements", employe, JsonContent.Create(new
        {
            productId = produit, warehouseId = entrepot, type = 1, quantity = 2,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task EtantDonneTypeEnMinuscules_QuandEnregistrer_AlorsAccepte()
    {
        // Un client qui écrit ses énumérations en camelCase (réglage par défaut de Refit) reste compris.
        var (gestionnaire, employe, source, produit) = await PreparerAsync();
        var destination = await _client.CreerEntrepotAsync(gestionnaire);

        var response = await _client.EnregistrerMouvementAsync(employe, produit, source, "transfer", 2, destination);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task EtantDonneEntrepotDesactive_QuandEntreeSortieOuTransfert_AlorsSeulLAjoutDeStockEstRefuse()
    {
        var (gestionnaire, employe, ferme, produit) = await PreparerAsync();
        var actif = await _client.CreerEntrepotAsync(gestionnaire);
        await _client.SendAsync(HttpMethod.Patch, $"/api/v1/warehouses/{ferme}/active", gestionnaire, JsonContent.Create(new { isActive = false }));

        var entree = await _client.EnregistrerMouvementAsync(employe, produit, ferme, "In", 1);
        var sortie = await _client.EnregistrerMouvementAsync(employe, produit, ferme, "Out", 2);
        var versActif = await _client.EnregistrerMouvementAsync(employe, produit, ferme, "Transfer", 3, actif);
        var versFerme = await _client.EnregistrerMouvementAsync(employe, produit, actif, "Transfer", 1, ferme);

        entree.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await entree.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail
            .Should().Be("L'entrepôt est désactivé : il ne peut plus recevoir de stock.");
        sortie.StatusCode.Should().Be(HttpStatusCode.Created);
        versActif.StatusCode.Should().Be(HttpStatusCode.Created);
        versFerme.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var stocks = await _client.LireAsync<List<StockDto>>($"/api/v1/stocks/product/{produit}", employe);
        stocks.Single(s => s.WarehouseId == ferme).Quantity.Should().Be(5);
        stocks.Single(s => s.WarehouseId == actif).Quantity.Should().Be(3);
    }

    [Fact]
    public async Task EtantDonneTypeInconnu_QuandEnregistrer_AlorsRequeteInvalide()
    {
        var (_, employe, entrepot, produit) = await PreparerAsync();

        var response = await _client.EnregistrerMouvementAsync(employe, produit, entrepot, "Vol", 2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EtantDonneQuantiteNulle_QuandEnregistrer_AlorsRequeteInvalideSansMouvement()
    {
        var (_, employe, entrepot, produit) = await PreparerAsync();

        var response = await _client.EnregistrerMouvementAsync(employe, produit, entrepot, "In", 0);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.LireAsync<PagedResult<StockMovementDto>>($"/api/v1/movements/product/{produit}", employe)).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task EtantDonneMouvementsSuccessifs_QuandLireLHistorique_AlorsPlusRecentsDAbordEtPagine()
    {
        var (_, employe, entrepot, produit) = await PreparerAsync();
        foreach (var quantite in new[] { 1, 2, 3 })
            (await _client.EnregistrerMouvementAsync(employe, produit, entrepot, "In", quantite)).StatusCode.Should().Be(HttpStatusCode.Created);

        var premierePage = await _client.LireAsync<PagedResult<StockMovementDto>>($"/api/v1/movements/product/{produit}?page=1&pageSize=2", employe);
        var secondePage = await _client.LireAsync<PagedResult<StockMovementDto>>($"/api/v1/movements/product/{produit}?page=2&pageSize=2", employe);

        premierePage.TotalCount.Should().Be(3);
        premierePage.Items.Select(m => m.Quantity).Should().Equal(3, 2);
        secondePage.Items.Select(m => m.Quantity).Should().Equal(1);
    }

    [Fact]
    public async Task EtantDonneTransfert_QuandLireLeJournalRecent_AlorsEnTeteAvecLesNomsDesEntrepots()
    {
        var gestionnaire = await RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);
        var sourceNom = DonneesDeTest.Unique("Source");
        var destinationNom = DonneesDeTest.Unique("Destination");
        var source = await _client.CreerEntrepotAsync(gestionnaire, sourceNom);
        var destination = await _client.CreerEntrepotAsync(gestionnaire, destinationNom);
        var sku = DonneesDeTest.Unique("JRN");
        var produit = await _client.CreerProduitAsync(gestionnaire, source, 8, sku, nom: "Journal témoin");

        var cree = await (await _client.EnregistrerMouvementAsync(gestionnaire, produit, source, "Transfer", 3, destination)).Content
            .ReadFromJsonAsync<Dictionary<string, Guid>>();
        var journal = await _client.LireAsync<List<RecentMovementDto>>("/api/v1/movements/recent?take=5", gestionnaire);

        journal.Should().HaveCountLessThanOrEqualTo(5);
        journal[0].Should().BeEquivalentTo(new
        {
            Id = cree!["id"], ProductSku = sku, ProductName = "Journal témoin",
            WarehouseName = sourceNom, ToWarehouseName = destinationNom, Type = MovementType.Transfer, Quantity = 3,
        });
    }

    [Fact]
    public async Task EtantDonneProduit_QuandExporterSonHistoriqueEnCsv_AlorsUneLigneParMouvementEnFrancais()
    {
        var (_, employe, entrepot, produit) = await PreparerAsync();
        await _client.EnregistrerMouvementAsync(employe, produit, entrepot, "In", 2, motif: "Réception fournisseur");
        await _client.EnregistrerMouvementAsync(employe, produit, entrepot, "Out", 1, motif: "Vente");

        var response = await _client.SendAsync(HttpMethod.Get, $"/api/v1/movements/export?format=Csv&productId={produit}", employe);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lignes = (await response.Content.ReadAsStringAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lignes.Should().HaveCount(3, "une ligne d'en-tête et deux mouvements");
        lignes.Skip(1).Should().Contain(l => l.Contains("Réception fournisseur")).And.Contain(l => l.Contains("Vente"));
    }

    [Fact]
    public async Task EtantDonneHistoriqueComplet_QuandExporterEnExcel_AlorsClasseurTelechargeable()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);

        var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/movements/export?format=Excel", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().StartWith("mouvements_").And.EndWith(".xlsx");
        var octets = await response.Content.ReadAsByteArrayAsync();
        octets.Take(2).Should().Equal((byte)'P', (byte)'K');
    }
}
