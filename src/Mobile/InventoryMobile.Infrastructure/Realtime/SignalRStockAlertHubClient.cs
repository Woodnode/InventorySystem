using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Notifications;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryMobile.Infrastructure.Realtime;

/// <summary>Implémentation de <see cref="IStockAlertHubClient"/> via le client .NET SignalR.</summary>
public sealed class SignalRStockAlertHubClient : IStockAlertHubClient
{
    private readonly HubConnection _connection;

    public event EventHandler<LowStockAlert>? LowStockAlertReceived;

    public SignalRStockAlertHubClient(string hubUrl, IAuthService authService)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Réévalué à chaque tentative de (re)connexion : capte toujours la session la
                // plus récente, jamais un token figé au moment de la construction.
                options.AccessTokenProvider = () => Task.FromResult(authService.CurrentSession?.AccessToken);
            })
            .WithAutomaticReconnect()
            .AddJsonProtocol(options => options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true)
            .Build();

        _connection.On<LowStockAlert>("LowStock", alert => LowStockAlertReceived?.Invoke(this, alert));
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_connection.State != HubConnectionState.Disconnected) return;
        await _connection.StartAsync(ct);
    }

    public async Task StopAsync()
    {
        if (_connection.State == HubConnectionState.Disconnected) return;
        await _connection.StopAsync();
    }
}
