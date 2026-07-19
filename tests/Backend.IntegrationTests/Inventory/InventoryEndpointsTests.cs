using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Movements.Commands;
using InventorySystem.Application.Stocks.Dtos;
using InventorySystem.Application.Warehouses.Commands;
using InventorySystem.Domain.Entities;

namespace InventorySystem.Backend.IntegrationTests.Inventory;

/// <summary>
/// Scénarios de bout en bout pour le stock multi-entrepôt : création avec stock initial,
/// mouvements entrée/sortie, transferts entre entrepôts — contre l'application réelle
/// (voir plan §10). Chaque test crée ses propres entrepôts/produits pour rester isolé.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class InventoryEndpointsTests
{
    private readonly HttpClient _client;

    public InventoryEndpointsTests(ApiFactory factory) => _client = factory.CreateClient();

    // 8 caractères hexadécimaux suffisent pour l'unicité en tests, tout en restant
    // largement sous les limites de longueur (SKU max 32, Nom max 200 — voir validators).
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginCommand(ApiFactory.AdminEmail, ApiFactory.AdminPassword));
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        return result!.AccessToken;
    }

    private static void Authorize(HttpRequestMessage request, string token)
        => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<Guid> CreateWarehouseAsync(string token, string? name = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/warehouses")
        {
            Content = JsonContent.Create(new CreateWarehouseCommand(name ?? Unique("Entrepot"), "123 rue Test")),
        };
        Authorize(request, token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        return body!["id"];
    }

    private async Task<Guid> CreateProductAsync(string token, Guid warehouseId, int initialQuantity, int lowStockThreshold = 2)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent.Create(new
            {
                sku = Unique("SKU"),
                name = "Produit de test",
                description = (string?)null,
                lowStockThreshold,
                warehouseId,
                initialQuantity,
                supplierId = (Guid?)null,
            }),
        };
        Authorize(request, token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        return body!["id"];
    }

    private async Task<HttpResponseMessage> RecordMovementAsync(
        string token, Guid productId, Guid warehouseId, MovementType type, int quantity, Guid? toWarehouseId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/movements")
        {
            Content = JsonContent.Create(new RecordMovementCommand(
                productId, warehouseId, type, quantity, "Test", toWarehouseId, Guid.NewGuid())),
        };
        Authorize(request, token);

        return await _client.SendAsync(request);
    }

    private async Task<IReadOnlyList<StockDto>> GetStockAsync(string token, Guid productId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stocks/product/{productId}");
        Authorize(request, token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<List<StockDto>>())!;
    }

    [Fact]
    public async Task CreateProduct_WithInitialQuantity_CreatesMatchingStockRow()
    {
        var token = await GetAdminTokenAsync();
        var warehouseId = await CreateWarehouseAsync(token);

        var productId = await CreateProductAsync(token, warehouseId, initialQuantity: 25);

        var stocks = await GetStockAsync(token, productId);
        stocks.Should().ContainSingle(s => s.WarehouseId == warehouseId && s.Quantity == 25);
    }

    [Fact]
    public async Task RecordMovement_In_IncreasesStockAtWarehouse()
    {
        var token = await GetAdminTokenAsync();
        var warehouseId = await CreateWarehouseAsync(token);
        var productId = await CreateProductAsync(token, warehouseId, initialQuantity: 10);

        var response = await RecordMovementAsync(token, productId, warehouseId, MovementType.In, 5);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var stocks = await GetStockAsync(token, productId);
        stocks.Single(s => s.WarehouseId == warehouseId).Quantity.Should().Be(15);
    }

    [Fact]
    public async Task RecordMovement_OutWithInsufficientStock_ReturnsBadRequest()
    {
        var token = await GetAdminTokenAsync();
        var warehouseId = await CreateWarehouseAsync(token);
        var productId = await CreateProductAsync(token, warehouseId, initialQuantity: 3);

        var response = await RecordMovementAsync(token, productId, warehouseId, MovementType.Out, 10);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var stocks = await GetStockAsync(token, productId);
        stocks.Single(s => s.WarehouseId == warehouseId).Quantity.Should().Be(3); // inchangé
    }

    [Fact]
    public async Task RecordMovement_Transfer_MovesQuantityBetweenWarehouses()
    {
        var token = await GetAdminTokenAsync();
        var sourceWarehouseId = await CreateWarehouseAsync(token);
        var destinationWarehouseId = await CreateWarehouseAsync(token);
        var productId = await CreateProductAsync(token, sourceWarehouseId, initialQuantity: 20);

        var response = await RecordMovementAsync(
            token, productId, sourceWarehouseId, MovementType.Transfer, 8, toWarehouseId: destinationWarehouseId);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var stocks = await GetStockAsync(token, productId);
        stocks.Single(s => s.WarehouseId == sourceWarehouseId).Quantity.Should().Be(12);
        stocks.Single(s => s.WarehouseId == destinationWarehouseId).Quantity.Should().Be(8);
    }

    [Fact]
    public async Task RecordMovement_TransferWithInsufficientStock_LeavesBothWarehousesUnchanged()
    {
        var token = await GetAdminTokenAsync();
        var sourceWarehouseId = await CreateWarehouseAsync(token);
        var destinationWarehouseId = await CreateWarehouseAsync(token);
        var productId = await CreateProductAsync(token, sourceWarehouseId, initialQuantity: 2);

        var response = await RecordMovementAsync(
            token, productId, sourceWarehouseId, MovementType.Transfer, 10, toWarehouseId: destinationWarehouseId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var stocks = await GetStockAsync(token, productId);
        stocks.Single(s => s.WarehouseId == sourceWarehouseId).Quantity.Should().Be(2);
        stocks.Should().NotContain(s => s.WarehouseId == destinationWarehouseId);
    }

    [Fact]
    public async Task RecordMovement_TransferToSameWarehouse_ReturnsBadRequest()
    {
        var token = await GetAdminTokenAsync();
        var warehouseId = await CreateWarehouseAsync(token);
        var productId = await CreateProductAsync(token, warehouseId, initialQuantity: 10);

        var response = await RecordMovementAsync(
            token, productId, warehouseId, MovementType.Transfer, 5, toWarehouseId: warehouseId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_WithNonExistentWarehouse_ReturnsBadRequest()
    {
        var token = await GetAdminTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent.Create(new
            {
                sku = Unique("SKU"),
                name = "Produit orphelin",
                description = (string?)null,
                lowStockThreshold = 2,
                warehouseId = Guid.NewGuid(), // n'existe pas
                initialQuantity = 5,
                supplierId = (Guid?)null,
            }),
        };
        Authorize(request, token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
