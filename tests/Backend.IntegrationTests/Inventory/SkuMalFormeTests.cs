using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using InventorySystem.Application.Products.Commands;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Backend.IntegrationTests.Inventory;

/// <summary>
/// Un SKU hors des bornes (moins de 3 ou plus de 32 caractères) est une erreur de saisie, pas
/// une panne : 400 au scan, et une seule ligne écartée à l'import.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SkuMalFormeTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public SkuMalFormeTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456")]
    public async Task EtantDonneSkuHorsBornes_QuandRechercherParSku_AlorsRequeteInvalideAvecLeMotif(string sku)
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);

        var response = await _client.SendAsync(HttpMethod.Get, $"/api/v1/products/by-sku/{sku}", token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var probleme = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        probleme!.Detail.Should().Be("Le SKU doit contenir entre 3 et 32 caractères.");
    }

    [Fact]
    public async Task EtantDonneImportAvecUneLigneAuSkuTropCourt_QuandImporter_AlorsSeuleCetteLigneEstEcartee()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);
        var valide = $"IMP-{Guid.NewGuid():N}"[..12];
        var csv = $"Code produit;Titre\nAB;Trop court\n{valide};Produit importé\n";
        using var contenu = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "catalogue.csv" },
        };

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/products/import", token, contenu);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resultat = await response.Content.ReadFromJsonAsync<ImportResult>();
        resultat!.ProductsCreated.Should().Be(1);
        resultat.Errors.Should().ContainSingle().Which.Should().Contain("AB");
        (await _client.SendAsync(HttpMethod.Get, $"/api/v1/products/by-sku/{valide}", token))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
