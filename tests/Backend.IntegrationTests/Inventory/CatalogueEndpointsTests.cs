using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Products.Commands;
using InventorySystem.Application.Products.Dtos;
using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;

namespace InventorySystem.Backend.IntegrationTests.Inventory;

/// <summary>
/// Routes du catalogue (/products) contre la vraie base : création, liste filtrée et triée,
/// fiche, scan par SKU, stock bas, collections, export et import, avec leurs erreurs.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class CatalogueEndpointsTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CatalogueEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task<string> GestionnaireAsync() => RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);

    private Task<string> EmployeAsync() => RoleTokens.GetAsync(_factory, RoleTokens.Employe);

    // --- Création ---

    [Fact]
    public async Task EtantDonneProduitCree_QuandSuivreLEnTeteLocation_AlorsRouteDeListe()
    {
        var token = await GestionnaireAsync();
        var entrepot = await _client.CreerEntrepotAsync(token);

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/products", token, JsonContent.Create(new
        {
            sku = DonneesDeTest.Unique("LOC"), name = "Avec Location", lowStockThreshold = 1, warehouseId = entrepot, initialQuantity = 0,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Contain("/api/v1/Products");
    }

    [Fact]
    public async Task EtantDonneSkuDejaUtilise_QuandCreer_AlorsConflitAvecMessageMetier()
    {
        var token = await GestionnaireAsync();
        var entrepot = await _client.CreerEntrepotAsync(token);
        var sku = DonneesDeTest.Unique("DUP");
        await _client.CreerProduitAsync(token, entrepot, 1, sku);

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/products", token, JsonContent.Create(new
        {
            sku = sku.ToLowerInvariant(), name = "Doublon", lowStockThreshold = 1, warehouseId = entrepot, initialQuantity = 0,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "le SKU est normalisé avant la contrainte d'unicité");
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail
            .Should().Be("Ce SKU est déjà utilisé par un autre produit.");
    }

    [Fact]
    public async Task EtantDonneChampsInvalides_QuandCreer_AlorsToutesLesErreursEnFrancaisDansUnSeulMessage()
    {
        var token = await GestionnaireAsync();

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/products", token, JsonContent.Create(new
        {
            sku = "AB", name = "", lowStockThreshold = -1, warehouseId = Guid.NewGuid(), initialQuantity = -5,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var detail = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail;
        detail.Should().Contain("L'entrepôt spécifié n'existe pas.").And.Contain(" | ");
        detail.Should().NotContainEquivalentOf("must", "les messages FluentValidation sont traduits");
    }

    // --- Liste ---

    [Fact]
    public async Task EtantDonneRecherche_QuandLister_AlorsSeulsLesProduitsCorrespondantsSurSkuOuNom()
    {
        var token = await GestionnaireAsync();
        var entrepot = await _client.CreerEntrepotAsync(token);
        var marqueur = DonneesDeTest.Unique("RCH");
        await _client.CreerProduitAsync(token, entrepot, 1, sku: $"{marqueur}-A");
        await _client.CreerProduitAsync(token, entrepot, 1, nom: $"Titre {marqueur.ToLowerInvariant()}");
        await _client.CreerProduitAsync(token, entrepot, 1);

        var page = await _client.LireAsync<PagedResult<ProductDto>>($"/api/v1/products?search={marqueur}", await EmployeAsync());

        page.TotalCount.Should().Be(2, "la recherche porte sur le SKU et le nom, sans tenir compte de la casse");
    }

    [Fact]
    public async Task EtantDonneTriParQuantiteDecroissante_QuandLister_AlorsOrdreDesStocksTotaux()
    {
        var token = await GestionnaireAsync();
        var entrepot = await _client.CreerEntrepotAsync(token);
        var marqueur = DonneesDeTest.Unique("TRI");
        foreach (var quantite in new[] { 5, 50, 0 })
            await _client.CreerProduitAsync(token, entrepot, quantite, sku: $"{marqueur}-{quantite}");

        var page = await _client.LireAsync<PagedResult<ProductDto>>(
            $"/api/v1/products?search={marqueur}&sortBy=Quantity&sortDescending=true", token);

        page.Items.Select(p => p.Quantity).Should().Equal(50, 5, 0);
    }

    [Fact]
    public async Task EtantDonneFiltresDeQuantiteEtStockBas_QuandLister_AlorsAppliquesAuTotal()
    {
        var token = await GestionnaireAsync();
        var entrepot = await _client.CreerEntrepotAsync(token);
        var marqueur = DonneesDeTest.Unique("FLT");
        await _client.CreerProduitAsync(token, entrepot, 1, sku: $"{marqueur}-BAS", seuil: 3);
        await _client.CreerProduitAsync(token, entrepot, 20, sku: $"{marqueur}-MOY", seuil: 3);
        await _client.CreerProduitAsync(token, entrepot, 90, sku: $"{marqueur}-HAUT", seuil: 3);

        var bas = await _client.LireAsync<PagedResult<ProductDto>>($"/api/v1/products?search={marqueur}&lowStockOnly=true", token);
        var plage = await _client.LireAsync<PagedResult<ProductDto>>($"/api/v1/products?search={marqueur}&minQuantity=10&maxQuantity=50", token);

        bas.Items.Should().ContainSingle().Which.Sku.Should().EndWith("-BAS");
        plage.Items.Should().ContainSingle().Which.Sku.Should().EndWith("-MOY");
    }

    [Fact]
    public async Task EtantDonneCollection_QuandFiltrer_AlorsCorrespondanceExacte()
    {
        var token = await GestionnaireAsync();
        var entrepot = await _client.CreerEntrepotAsync(token);
        var collection = DonneesDeTest.Unique("Collection");
        await _client.CreerProduitAsync(token, entrepot, 1, collection: collection);
        await _client.CreerProduitAsync(token, entrepot, 1, collection: collection + " bis");

        var page = await _client.LireAsync<PagedResult<ProductDto>>($"/api/v1/products?collection={Uri.EscapeDataString(collection)}", token);
        var collections = await _client.LireAsync<List<CollectionDto>>("/api/v1/products/collections", token);

        page.TotalCount.Should().Be(1);
        collections.Should().Contain(new CollectionDto(collection, 1));
    }

    [Fact]
    public async Task EtantDonneTaillesDePageExtremes_QuandLister_AlorsBornees()
    {
        var token = await EmployeAsync();

        var page = await _client.LireAsync<PagedResult<ProductDto>>("/api/v1/products?page=-3&pageSize=100000", token);

        page.Page.Should().Be(1);
        page.PageSize.Should().Be(100);
        page.Items.Count.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task EtantDonneProduitDansDeuxEntrepots_QuandLister_AlorsEmplacementPrincipalEtTotal()
    {
        var token = await GestionnaireAsync();
        var petit = await _client.CreerEntrepotAsync(token);
        var grandNom = DonneesDeTest.Unique("Grand");
        var grand = await _client.CreerEntrepotAsync(token, grandNom);
        var sku = DonneesDeTest.Unique("EMP");
        var produit = await _client.CreerProduitAsync(token, petit, 2, sku);
        (await _client.EnregistrerMouvementAsync(token, produit, grand, "In", 30)).StatusCode.Should().Be(HttpStatusCode.Created);

        var page = await _client.LireAsync<PagedResult<ProductDto>>($"/api/v1/products?search={sku}", token);

        page.Items.Single().Quantity.Should().Be(32);
        page.Items.Single().PrimaryLocation.Should().Be(grandNom);
    }

    // --- Fiche, scan, stock bas ---

    [Fact]
    public async Task EtantDonneProduitExistant_QuandLireSaFiche_AlorsStockTotalEtIndicateur()
    {
        var token = await GestionnaireAsync();
        var produit = await _client.CreerProduitAsync(token, await _client.CreerEntrepotAsync(token), 2, seuil: 5);

        var fiche = await _client.LireAsync<ProductDto>($"/api/v1/products/{produit}", await EmployeAsync());

        fiche.Id.Should().Be(produit);
        fiche.Quantity.Should().Be(2);
        fiche.IsLowOnStock.Should().BeTrue();
    }

    [Fact]
    public async Task EtantDonneSkuSaisiEnMinusculesAvecEspaces_QuandScanner_AlorsProduitTrouve()
    {
        var token = await GestionnaireAsync();
        var sku = DonneesDeTest.Unique("SCN");
        var produit = await _client.CreerProduitAsync(token, await _client.CreerEntrepotAsync(token), 4, sku);

        var fiche = await _client.LireAsync<ProductDto>($"/api/v1/products/by-sku/{Uri.EscapeDataString($" {sku.ToLowerInvariant()} ")}", token);

        fiche.Id.Should().Be(produit);
    }

    [Fact]
    public async Task EtantDonneProduitSousLeSeuil_QuandListerLeStockBas_AlorsChaqueLigneEstBasse()
    {
        var token = await GestionnaireAsync();
        await _client.CreerProduitAsync(token, await _client.CreerEntrepotAsync(token), 0, seuil: 10);

        var page = await _client.LireAsync<PagedResult<ProductDto>>("/api/v1/products/low-stock?pageSize=100", token);

        page.TotalCount.Should().BeGreaterThan(0);
        page.Items.Should().OnlyContain(p => p.IsLowOnStock && p.Quantity <= p.LowStockThreshold);
    }

    // --- Export ---

    [Theory]
    [InlineData("Csv", "text/csv", ".csv")]
    [InlineData("Excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx")]
    public async Task EtantDonneFormat_QuandExporterLeCatalogue_AlorsFichierTelechargeableDuBonType(string format, string type, string extension)
    {
        var token = await EmployeAsync();

        var response = await _client.SendAsync(HttpMethod.Get, $"/api/v1/products/export?format={format}", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(type);
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().EndWith(extension);
        (await response.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
        response.Headers.Contains("X-Export-Truncated").Should().BeFalse();
    }

    [Fact]
    public async Task EtantDonneProduitCree_QuandExporterEnCsv_AlorsSaLigneFigureDansLeFichier()
    {
        var token = await GestionnaireAsync();
        var sku = DonneesDeTest.Unique("EXP");
        await _client.CreerProduitAsync(token, await _client.CreerEntrepotAsync(token), 7, sku, nom: "Export témoin");

        var csv = await (await _client.SendAsync(HttpMethod.Get, "/api/v1/products/export?format=Csv", token)).Content.ReadAsStringAsync();

        csv.Split('\n').Should().Contain(l => l.Contains(sku) && l.Contains("Export témoin"));
    }

    // --- Import ---

    private async Task<HttpResponseMessage> ImporterAsync(byte[] contenu, string nomFichier)
    {
        using var formulaire = new MultipartFormDataContent { { new ByteArrayContent(contenu), "file", nomFichier } };
        return await _client.SendAsync(HttpMethod.Post, "/api/v1/products/import", await GestionnaireAsync(), formulaire);
    }

    [Fact]
    public async Task EtantDonneCsvValide_QuandImporter_AlorsProduitsEtStockConsultables()
    {
        var sku = DonneesDeTest.Unique("IMP");
        var entrepot = DonneesDeTest.Unique("Depot");
        var csv = $"Code produit;Titre;Location;Nbre Copies;Section\n{sku};Livre importé;{entrepot};9;C\n";

        var response = await ImporterAsync(Encoding.UTF8.GetBytes(csv), "catalogue.csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resultat = await response.Content.ReadFromJsonAsync<ImportResult>();
        resultat.Should().BeEquivalentTo(new ImportResult(1, 0, []));
        var token = await GestionnaireAsync();
        var fiche = await _client.LireAsync<ProductDto>($"/api/v1/products/by-sku/{sku}", token);
        fiche.Quantity.Should().Be(9);
        fiche.Name.Should().Be("Livre importé");
    }

    [Fact]
    public async Task EtantDonneMemeFichierImporteDeuxFois_QuandReimporter_AlorsMisAJourSansDoublerLeStock()
    {
        var sku = DonneesDeTest.Unique("REI");
        var csv = Encoding.UTF8.GetBytes($"Code produit;Titre;Location;Nbre Copies\n{sku};Premier titre;{DonneesDeTest.Unique("Depot")};5\n");
        await ImporterAsync(csv, "catalogue.csv");

        var response = await ImporterAsync(csv, "catalogue.csv");

        (await response.Content.ReadFromJsonAsync<ImportResult>()).Should().BeEquivalentTo(new ImportResult(0, 1, []));
        (await _client.LireAsync<ProductDto>($"/api/v1/products/by-sku/{sku}", await GestionnaireAsync())).Quantity.Should().Be(5);
    }

    [Fact]
    public async Task EtantDonneClasseurExcel_QuandImporter_AlorsLignesLues()
    {
        var sku = DonneesDeTest.Unique("XLS");
        using var flux = new MemoryStream();
        flux.SaveAs(new List<Dictionary<string, object>>
        {
            new() { ["Code produit"] = sku, ["Titre"] = "Depuis Excel", ["Année"] = 2024 },
        });

        var response = await ImporterAsync(flux.ToArray(), "catalogue.xlsx");

        (await response.Content.ReadFromJsonAsync<ImportResult>())!.ProductsCreated.Should().Be(1);
        (await _client.LireAsync<ProductDto>($"/api/v1/products/by-sku/{sku}", await GestionnaireAsync())).Year.Should().Be(2024);
    }

    [Fact]
    public async Task EtantDonneFichierVide_QuandImporter_AlorsRequeteInvalide()
    {
        var response = await ImporterAsync([], "vide.csv");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Fichier manquant ou vide.");
    }

    [Fact]
    public async Task EtantDonneFichierDePlusDeCinqMo_QuandImporter_AlorsRefuseAvantLecture()
    {
        var response = await ImporterAsync(new byte[ImportLimits.MaxFileBytes + 1], "gros.csv");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("5 Mo au plus");
    }

    [Fact]
    public async Task EtantDonneFauxClasseur_QuandImporter_AlorsMessageDansLeResultatSansErreurServeur()
    {
        var response = await ImporterAsync(Encoding.UTF8.GetBytes("pas un classeur"), "faux.xlsx");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ImportResult>())!.Errors
            .Should().ContainSingle().Which.Should().Be("Le fichier n'est pas un classeur Excel valide.");
    }
}
