using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace InventorySystem.Backend.IntegrationTests.Inventory;

/// <summary>
/// Création de données propres à un test, par l'API elle-même. Chaque nom porte un suffixe
/// aléatoire : les tests partagent la base (et le jeu de démonstration) sans se gêner.
/// </summary>
internal static class DonneesDeTest
{
    /// <summary>Options de lecture alignées sur l'API (énumérations en texte).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Suffixe court et unique ; reste sous les longueurs maximales (SKU 32, nom 200).</summary>
    public static string Unique(string prefixe) => $"{prefixe}-{Guid.NewGuid():N}"[..(prefixe.Length + 9)].ToUpperInvariant();

    public static async Task<Guid> IdCreeAsync(this HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>())!["id"];
    }

    public static async Task<Guid> CreerEntrepotAsync(this HttpClient client, string token, string? nom = null)
        => await (await client.SendAsync(HttpMethod.Post, "/api/v1/warehouses", token,
            JsonContent.Create(new { name = nom ?? Unique("Entrepot"), address = "12 rue des Essais" }))).IdCreeAsync();

    public static async Task<Guid> CreerFournisseurAsync(this HttpClient client, string token, string? nom = null)
        => await (await client.SendAsync(HttpMethod.Post, "/api/v1/suppliers", token,
            JsonContent.Create(new { name = nom ?? Unique("Fournisseur"), contactEmail = "contact@exemple.test", phone = "418 555-0100" }))).IdCreeAsync();

    public static async Task<Guid> CreerProduitAsync(
        this HttpClient client, string token, Guid entrepot, int quantite, string? sku = null, string? nom = null,
        int seuil = 2, string? collection = null, decimal? poidsGrammes = null)
        => await (await client.SendAsync(HttpMethod.Post, "/api/v1/products", token, JsonContent.Create(new
        {
            sku = sku ?? Unique("SKU"),
            name = nom ?? "Produit de test",
            description = (string?)null,
            lowStockThreshold = seuil,
            warehouseId = entrepot,
            initialQuantity = quantite,
            supplierId = (Guid?)null,
            collection,
            weightPerCopyGrams = poidsGrammes,
        }))).IdCreeAsync();

    public static Task<HttpResponseMessage> EnregistrerMouvementAsync(
        this HttpClient client, string token, Guid produit, Guid entrepot, string type, int quantite,
        Guid? destination = null, Guid? clientGuid = null, string? motif = "Test")
        => client.SendAsync(HttpMethod.Post, "/api/v1/movements", token, JsonContent.Create(new
        {
            productId = produit,
            warehouseId = entrepot,
            type,
            quantity = quantite,
            reason = motif,
            toWarehouseId = destination,
            clientGuid,
        }));

    public static async Task<T> LireAsync<T>(this HttpClient client, string url, string token)
    {
        var response = await client.SendAsync(HttpMethod.Get, url, token);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
