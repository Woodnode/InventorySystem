using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Auth.Dtos;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace InventorySystem.Backend.IntegrationTests.Realtime;

/// <summary>
/// Connexion au hub de stock telle que la fait le navigateur : WebSocket direct, JWT en
/// query string ("access_token"), sans en-tête Authorization. La négociation est sautée
/// pour que la seule requête envoyée soit l'upgrade WebSocket ; si le serveur ne relit
/// pas le jeton sur ce chemin, il répond 401 et la connexion échoue.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class StockHubConnectionTests
{
    private const string HubPath = "/api/v1/hubs/stock";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public StockHubConnectionTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginCommand(ApiFactory.AdminEmail, ApiFactory.AdminPassword));
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        return result!.AccessToken;
    }

    private HubConnection BuildConnection(string query)
    {
        var url = new Uri(_factory.Server.BaseAddress, HubPath + query);

        return new HubConnectionBuilder()
            .WithUrl(url, HttpTransportType.WebSockets, options =>
            {
                options.SkipNegotiation = true;
                // Le TestServer n'ouvre pas de vrai port : la WebSocket passe par son client interne.
                options.WebSocketFactory = async (context, cancellationToken) =>
                    await _factory.Server.CreateWebSocketClient().ConnectAsync(context.Uri, cancellationToken);
            })
            .Build();
    }

    [Fact]
    public async Task EtantDonneJetonEnQueryString_QuandConnexionWebSocketAuHub_AlorsConnexionEtablie()
    {
        var token = await GetAdminTokenAsync();
        await using var connection = BuildConnection($"?access_token={Uri.EscapeDataString(token)}");

        await connection.StartAsync();

        connection.State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task EtantDonneAucunJeton_QuandConnexionWebSocketAuHub_AlorsConnexionRefusee()
    {
        await using var connection = BuildConnection(string.Empty);

        var act = () => connection.StartAsync();

        await act.Should().ThrowAsync<Exception>();
        connection.State.Should().Be(HubConnectionState.Disconnected);
    }
}
