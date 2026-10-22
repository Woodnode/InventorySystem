using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Application.Suppliers.Dtos;
using InventorySystem.Application.Warehouses.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Backend.IntegrationTests.Inventory;

/// <summary>
/// Routes des référentiels (/warehouses, /suppliers) : création, liste, modification,
/// désactivation puis réactivation, et refus des saisies invalides.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ReferentielsEndpointsTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ReferentielsEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task<string> GestionnaireAsync() => RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);

    // --- Entrepôts ---

    [Fact]
    public async Task EtantDonneEntrepotCree_QuandLister_AlorsPresentEtActif()
    {
        var token = await GestionnaireAsync();
        var nom = DonneesDeTest.Unique("Entrepot");

        var id = await _client.CreerEntrepotAsync(token, nom);

        var entrepots = await _client.LireAsync<List<WarehouseDto>>("/api/v1/warehouses", await RoleTokens.GetAsync(_factory, RoleTokens.Employe));
        entrepots.Should().ContainSingle(w => w.Id == id).Which.Should().Be(new WarehouseDto(id, nom, "12 rue des Essais", true));
    }

    [Fact]
    public async Task EtantDonneEntrepotsListes_QuandLireLOrdre_AlorsAlphabetique()
    {
        var entrepots = await _client.LireAsync<List<WarehouseDto>>("/api/v1/warehouses", await GestionnaireAsync());

        entrepots.Select(w => w.Name).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task EtantDonneEntrepotExistant_QuandLeRenommer_AlorsNouveauNomEtAdresse()
    {
        var token = await GestionnaireAsync();
        var id = await _client.CreerEntrepotAsync(token);
        var nouveauNom = DonneesDeTest.Unique("Renomme");

        var response = await _client.SendAsync(HttpMethod.Put, $"/api/v1/warehouses/{id}", token,
            JsonContent.Create(new { name = nouveauNom, address = (string?)null }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.LireAsync<List<WarehouseDto>>("/api/v1/warehouses", token))
            .Single(w => w.Id == id).Should().Be(new WarehouseDto(id, nouveauNom, null, true));
    }

    [Fact]
    public async Task EtantDonneEntrepotAvecStock_QuandDesactiverPuisReactiver_AlorsEtatSuiviEtStockConserve()
    {
        var token = await GestionnaireAsync();
        var id = await _client.CreerEntrepotAsync(token);
        var produit = await _client.CreerProduitAsync(token, id, 6);

        var desactivation = await _client.SendAsync(HttpMethod.Patch, $"/api/v1/warehouses/{id}/active", token, JsonContent.Create(new { isActive = false }));
        var apres = await _client.LireAsync<List<WarehouseDto>>("/api/v1/warehouses", token);
        var stock = await _client.LireAsync<List<InventorySystem.Application.Stocks.Dtos.StockDto>>($"/api/v1/stocks/product/{produit}", token);
        var reactivation = await _client.SendAsync(HttpMethod.Patch, $"/api/v1/warehouses/{id}/active", token, JsonContent.Create(new { isActive = true }));

        desactivation.StatusCode.Should().Be(HttpStatusCode.NoContent);
        apres.Single(w => w.Id == id).IsActive.Should().BeFalse();
        stock.Single().Quantity.Should().Be(6, "désactiver un entrepôt ne supprime ni ne vide son stock");
        reactivation.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.LireAsync<List<WarehouseDto>>("/api/v1/warehouses", token)).Single(w => w.Id == id).IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EtantDonneNomVide_QuandCreerUnEntrepot_AlorsRequeteInvalide(string nom)
    {
        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/warehouses", await GestionnaireAsync(),
            JsonContent.Create(new { name = nom, address = (string?)null }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EtantDonneNomVide_QuandRenommerUnEntrepot_AlorsRequeteInvalideEtNomInchange()
    {
        var token = await GestionnaireAsync();
        var nom = DonneesDeTest.Unique("Garde");
        var id = await _client.CreerEntrepotAsync(token, nom);

        var response = await _client.SendAsync(HttpMethod.Put, $"/api/v1/warehouses/{id}", token, JsonContent.Create(new { name = "" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.LireAsync<List<WarehouseDto>>("/api/v1/warehouses", token)).Single(w => w.Id == id).Name.Should().Be(nom);
    }

    // --- Fournisseurs ---

    [Fact]
    public async Task EtantDonneFournisseurCree_QuandLister_AlorsPresentAvecSesCoordonnees()
    {
        var token = await GestionnaireAsync();
        var nom = DonneesDeTest.Unique("Fournisseur");

        var id = await _client.CreerFournisseurAsync(token, nom);

        (await _client.LireAsync<List<SupplierDto>>("/api/v1/suppliers", token))
            .Should().ContainSingle(s => s.Id == id)
            .Which.Should().Be(new SupplierDto(id, nom, "contact@exemple.test", "418 555-0100", true));
    }

    [Fact]
    public async Task EtantDonneFournisseurExistant_QuandModifier_AlorsCoordonneesRemplacees()
    {
        var token = await GestionnaireAsync();
        var id = await _client.CreerFournisseurAsync(token);
        var nom = DonneesDeTest.Unique("Modifie");

        var response = await _client.SendAsync(HttpMethod.Put, $"/api/v1/suppliers/{id}", token,
            JsonContent.Create(new { name = nom, contactEmail = "ventes@exemple.test", phone = (string?)null }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.LireAsync<List<SupplierDto>>("/api/v1/suppliers", token))
            .Single(s => s.Id == id).Should().Be(new SupplierDto(id, nom, "ventes@exemple.test", null, true));
    }

    [Fact]
    public async Task EtantDonneFournisseurActif_QuandDesactiver_AlorsToujoursListeMaisInactif()
    {
        var token = await GestionnaireAsync();
        var id = await _client.CreerFournisseurAsync(token);

        var response = await _client.SendAsync(HttpMethod.Patch, $"/api/v1/suppliers/{id}/active", token, JsonContent.Create(new { isActive = false }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.LireAsync<List<SupplierDto>>("/api/v1/suppliers", token)).Single(s => s.Id == id).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task EtantDonneCourrielInvalide_QuandCreerUnFournisseur_AlorsMessageEnFrancais()
    {
        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/suppliers", await GestionnaireAsync(),
            JsonContent.Create(new { name = "Fournisseur", contactEmail = "pas-un-courriel" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().NotContainEquivalentOf("is not a valid");
    }

    [Fact]
    public async Task EtantDonneProduitRattacheAUnFournisseur_QuandCreer_AlorsAccepte()
    {
        var token = await GestionnaireAsync();
        var fournisseur = await _client.CreerFournisseurAsync(token);
        var entrepot = await _client.CreerEntrepotAsync(token);

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/products", token, JsonContent.Create(new
        {
            sku = DonneesDeTest.Unique("FRN"), name = "Avec fournisseur", lowStockThreshold = 1,
            warehouseId = entrepot, initialQuantity = 1, supplierId = fournisseur,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task EtantDonneEntrepotOuFournisseurDesactive_QuandCreerUnProduit_AlorsRequeteInvalide()
    {
        var token = await GestionnaireAsync();
        var entrepot = await _client.CreerEntrepotAsync(token);
        var fournisseur = await _client.CreerFournisseurAsync(token);
        var actif = await _client.CreerEntrepotAsync(token);
        await _client.SendAsync(HttpMethod.Patch, $"/api/v1/warehouses/{entrepot}/active", token, JsonContent.Create(new { isActive = false }));
        await _client.SendAsync(HttpMethod.Patch, $"/api/v1/suppliers/{fournisseur}/active", token, JsonContent.Create(new { isActive = false }));

        var dansEntrepotFerme = await _client.SendAsync(HttpMethod.Post, "/api/v1/products", token, JsonContent.Create(new
        {
            sku = DonneesDeTest.Unique("FER"), name = "Entrepôt fermé", lowStockThreshold = 1, warehouseId = entrepot, initialQuantity = 1,
        }));
        var chezFournisseurInactif = await _client.SendAsync(HttpMethod.Post, "/api/v1/products", token, JsonContent.Create(new
        {
            sku = DonneesDeTest.Unique("INA"), name = "Fournisseur inactif", lowStockThreshold = 1, warehouseId = actif,
            initialQuantity = 1, supplierId = fournisseur,
        }));

        (await dansEntrepotFerme.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Be("L'entrepôt spécifié est désactivé.");
        (await chezFournisseurInactif.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Be("Le fournisseur spécifié est désactivé.");
    }

    [Fact]
    public async Task EtantDonneFournisseurInconnu_QuandCreerUnProduit_AlorsRequeteInvalide()
    {
        var token = await GestionnaireAsync();
        var entrepot = await _client.CreerEntrepotAsync(token);

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/products", token, JsonContent.Create(new
        {
            sku = DonneesDeTest.Unique("FRX"), name = "Fournisseur fantôme", lowStockThreshold = 1,
            warehouseId = entrepot, initialQuantity = 1, supplierId = Guid.NewGuid(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Be("Le fournisseur spécifié n'existe pas.");
    }
}
