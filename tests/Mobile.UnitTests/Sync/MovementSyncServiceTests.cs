using System.Net;
using FluentAssertions;
using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Connectivity;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Persistence;
using InventoryMobile.Infrastructure.Sync;
using Moq;
using Refit;
using Xunit;

namespace InventoryMobile.Mobile.UnitTests.Sync;

/// <summary>
/// MovementSyncService contient la logique la plus subtile du mode hors-ligne (boucle avec
/// branches silencieuses : succès / rejet serveur / panne réseau). Testé ici en isolation
/// (Moq), sans SQLite ni appel réseau réel.
/// </summary>
public sealed class MovementSyncServiceTests
{
    private readonly Mock<ILocalMovementQueue> _queue = new();
    private readonly Mock<IInventoryApi> _api = new();
    private readonly Mock<IConnectivityChecker> _connectivity = new();
    private readonly Mock<IAuthService> _authService = new();

    private static readonly AuthSession Session =
        new("token", DateTime.UtcNow.AddHours(1), "refresh", "user@test.local", "Utilisateur Test", ["Employe"]);

    public MovementSyncServiceTests()
    {
        _authService.Setup(a => a.CurrentSession).Returns(Session);
        _connectivity.Setup(c => c.IsConnected).Returns(true);
    }

    private MovementSyncService CreateService()
        => new(_queue.Object, _api.Object, _connectivity.Object, _authService.Object);

    private static LocalMovement NewLocalMovement() => new()
    {
        ClientGuid = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        WarehouseId = Guid.NewGuid(),
        Type = (int)MobileMovementType.In,
        Quantity = 3,
    };

    private static async Task<ApiException> CreateApiExceptionAsync(HttpStatusCode statusCode)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/movements");
        var response = new HttpResponseMessage(statusCode) { RequestMessage = request };
        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    [Fact]
    public async Task SyncPendingAsync_NoSession_ReturnsZeroSyncedAndDoesNotCallApi()
    {
        _authService.Setup(a => a.CurrentSession).Returns((AuthSession?)null);
        _queue.Setup(q => q.GetPendingCountAsync()).ReturnsAsync(2);

        var result = await CreateService().SyncPendingAsync();

        result.SyncedCount.Should().Be(0);
        result.RemainingPendingCount.Should().Be(2);
        _api.Verify(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncPendingAsync_NotConnected_ReturnsZeroSyncedAndDoesNotCallApi()
    {
        _connectivity.Setup(c => c.IsConnected).Returns(false);
        _queue.Setup(q => q.GetPendingCountAsync()).ReturnsAsync(1);

        var result = await CreateService().SyncPendingAsync();

        result.SyncedCount.Should().Be(0);
        result.RemainingPendingCount.Should().Be(1);
        _api.Verify(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncPendingAsync_PendingMovementSucceeds_MarksSyncedAndIncrementsCount()
    {
        var movement = NewLocalMovement();
        _queue.Setup(q => q.GetPendingAsync()).ReturnsAsync([movement]);
        _api.Setup(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecordMovementResult(Guid.NewGuid()));

        var result = await CreateService().SyncPendingAsync();

        result.SyncedCount.Should().Be(1);
        result.RemainingPendingCount.Should().Be(0);
        _queue.Verify(q => q.MarkSyncedAsync(movement.ClientGuid), Times.Once);
    }

    [Fact]
    public async Task SyncPendingAsync_OneItemRejectedByServer_DiscardsItAndContinuesToNext()
    {
        var rejected = NewLocalMovement();
        var accepted = NewLocalMovement();
        _queue.Setup(q => q.GetPendingAsync()).ReturnsAsync([rejected, accepted]);

        var apiException = await CreateApiExceptionAsync(HttpStatusCode.BadRequest);
        _api.SetupSequence(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(apiException)
            .ReturnsAsync(new RecordMovementResult(Guid.NewGuid()));

        var result = await CreateService().SyncPendingAsync();

        // BadRequest = rejet définitif → retiré de la file (anti-poison) + suite du sync.
        result.SyncedCount.Should().Be(1);
        result.RemainingPendingCount.Should().Be(0);
        _queue.Verify(q => q.MarkSyncedAsync(rejected.ClientGuid), Times.Once);
        _queue.Verify(q => q.MarkSyncedAsync(accepted.ClientGuid), Times.Once);
    }

    [Fact]
    public async Task SyncPendingAsync_TransportFailure_BreaksLoopAndLeavesRemainingPending()
    {
        var failing = NewLocalMovement();
        var neverAttempted = NewLocalMovement();
        _queue.Setup(q => q.GetPendingAsync()).ReturnsAsync([failing, neverAttempted]);

        _api.Setup(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Aucune réponse du serveur."));

        var result = await CreateService().SyncPendingAsync();

        result.SyncedCount.Should().Be(0);
        result.RemainingPendingCount.Should().Be(2);
        _api.Verify(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _queue.Verify(q => q.MarkSyncedAsync(It.IsAny<Guid>()), Times.Never);
    }
}
