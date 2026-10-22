using System.Net;
using FluentAssertions;
using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Connectivity;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Persistence;
using InventoryMobile.Infrastructure.Sync;
using Moq;
using Refit;

namespace InventoryMobile.Mobile.UnitTests.Sync;

/// <summary>
/// Réponses du serveur pendant la synchronisation : session perdue (arrêt immédiat), conflit
/// (retiré de la file), panne serveur (gardé pour plus tard), et contenu exact de la requête.
/// </summary>
public sealed class MovementSyncServiceCasLimitesTests
{
    private readonly Mock<ILocalMovementQueue> _queue = new();
    private readonly Mock<IInventoryApi> _api = new();
    private readonly Mock<IConnectivityChecker> _connectivity = new();
    private readonly Mock<IAuthService> _authService = new();

    public MovementSyncServiceCasLimitesTests()
    {
        _authService.Setup(a => a.CurrentSession)
            .Returns(new AuthSession("acces", DateTime.UtcNow.AddHours(1), "refresh", "e@test.local", "Émile", ["Employe"]));
        _connectivity.Setup(c => c.IsConnected).Returns(true);
    }

    private MovementSyncService Creer() => new(_queue.Object, _api.Object, _connectivity.Object, _authService.Object);

    private static LocalMovement Mouvement() => new()
    {
        ClientGuid = Guid.NewGuid(), ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(),
        Type = (int)MobileMovementType.Out, Quantity = 2,
    };

    private static async Task<ApiException> ErreurAsync(HttpStatusCode statut)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "http://api.test/api/v1/movements");
        return await ApiException.Create(request, HttpMethod.Post, new HttpResponseMessage(statut) { RequestMessage = request }, new RefitSettings());
    }

    private void File(params LocalMovement[] mouvements) => _queue.Setup(q => q.GetPendingAsync()).ReturnsAsync(mouvements);

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task EtantDonneSessionRefuseeParLeServeur_QuandSynchroniser_AlorsArretSansRienRetirer(HttpStatusCode statut)
    {
        File(Mouvement(), Mouvement());
        _api.Setup(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(await ErreurAsync(statut));

        var result = await Creer().SyncPendingAsync();

        result.Should().Be(new SyncResult(0, 2));
        _api.Verify(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _queue.Verify(q => q.MarkSyncedAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneConflit_QuandSynchroniser_AlorsRetireDeLaFileCommeUnRejetDefinitif()
    {
        var conflit = Mouvement();
        File(conflit);
        _api.Setup(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(await ErreurAsync(HttpStatusCode.Conflict));

        var result = await Creer().SyncPendingAsync();

        result.Should().Be(new SyncResult(0, 0));
        _queue.Verify(q => q.MarkSyncedAsync(conflit.ClientGuid), Times.Once);
    }

    [Fact]
    public async Task EtantDonnePanneServeur_QuandSynchroniser_AlorsGardeEnFileEtPasseAuSuivant()
    {
        var enPanne = Mouvement();
        var suivant = Mouvement();
        File(enPanne, suivant);
        _api.SetupSequence(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(await ErreurAsync(HttpStatusCode.InternalServerError))
            .ReturnsAsync(new RecordMovementResult(Guid.NewGuid()));

        var result = await Creer().SyncPendingAsync();

        result.Should().Be(new SyncResult(1, 1));
        _queue.Verify(q => q.MarkSyncedAsync(enPanne.ClientGuid), Times.Never);
        _queue.Verify(q => q.MarkSyncedAsync(suivant.ClientGuid), Times.Once);
    }

    [Fact]
    public async Task EtantDonneTransfertEnFile_QuandSynchroniser_AlorsRequeteFideleAvecLIdentifiantClient()
    {
        var transfert = new LocalMovement
        {
            ClientGuid = Guid.NewGuid(), ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), ToWarehouseId = Guid.NewGuid(),
            Type = (int)MobileMovementType.Transfer, Quantity = 7, Reason = "Rééquilibrage",
        };
        File(transfert);
        RecordMovementRequest? envoye = null;
        _api.Setup(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordMovementRequest, CancellationToken>((r, _) => envoye = r)
            .ReturnsAsync(new RecordMovementResult(Guid.NewGuid()));

        await Creer().SyncPendingAsync();

        envoye.Should().Be(new RecordMovementRequest(
            transfert.ProductId, transfert.WarehouseId, MobileMovementType.Transfer, 7, "Rééquilibrage",
            transfert.ToWarehouseId, transfert.ClientGuid));
    }

    [Fact]
    public async Task EtantDonneFileVide_QuandSynchroniser_AlorsAucunAppel()
    {
        File();

        var result = await Creer().SyncPendingAsync();

        result.Should().Be(new SyncResult(0, 0));
        _api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EtantDonneSynchronisationAnnulee_QuandParcourirLaFile_AlorsArretAvantLEnvoi()
    {
        File(Mouvement());
        using var annulation = new CancellationTokenSource();
        annulation.Cancel();

        var act = () => Creer().SyncPendingAsync(annulation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _api.Verify(a => a.RecordMovementAsync(It.IsAny<RecordMovementRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
