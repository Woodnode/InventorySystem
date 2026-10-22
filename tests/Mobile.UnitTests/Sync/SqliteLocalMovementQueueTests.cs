using FluentAssertions;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Persistence;
using SQLite;

namespace InventoryMobile.Mobile.UnitTests.Sync;

/// <summary>
/// File locale des mouvements hors ligne, sur une vraie base SQLite (un fichier par test) :
/// ce qui est mis en file survit à un redémarrage et ressort dans l'ordre de saisie.
/// </summary>
public sealed class SqliteLocalMovementQueueTests : IDisposable
{
    private readonly string _chemin = Path.Combine(Path.GetTempPath(), $"file-mouvements-{Guid.NewGuid():N}.db3");

    public void Dispose()
    {
        SQLiteAsyncConnection.ResetPool();
        try { File.Delete(_chemin); } catch (IOException) { }
    }

    private static readonly DateTime Origine = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private static LocalMovement Mouvement(int minutes, MobileMovementType type = MobileMovementType.In, Guid? destination = null) => new()
    {
        ClientGuid = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        WarehouseId = Guid.NewGuid(),
        ToWarehouseId = destination,
        Type = (int)type,
        Quantity = 3,
        Reason = "Réception",
        CreatedAtUtc = Origine.AddMinutes(minutes),
    };

    [Fact]
    public async Task EtantDonneFileNeuve_QuandCompter_AlorsVide()
    {
        var file = new SqliteLocalMovementQueue(_chemin);

        (await file.GetPendingCountAsync()).Should().Be(0);
        (await file.GetPendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EtantDonneTransfertMisEnFile_QuandLeRelire_AlorsTousLesChampsConserves()
    {
        var file = new SqliteLocalMovementQueue(_chemin);
        var transfert = Mouvement(0, MobileMovementType.Transfer, Guid.NewGuid());

        await file.EnqueueAsync(transfert);

        var relu = (await file.GetPendingAsync()).Single();
        relu.Should().BeEquivalentTo(transfert, o => o.Excluding(m => m.CreatedAtUtc));
        relu.CreatedAtUtc.Should().BeCloseTo(transfert.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
        relu.IsSynced.Should().BeFalse();
    }

    [Fact]
    public async Task EtantDonneMouvementsSaisisDansLDesordre_QuandLireLaFile_AlorsDuPlusAncienAuPlusRecent()
    {
        // L'ordre compte : une sortie rejouée avant l'entrée qui la précède serait refusée.
        var file = new SqliteLocalMovementQueue(_chemin);
        var troisieme = Mouvement(30);
        var premier = Mouvement(10);
        var deuxieme = Mouvement(20);
        foreach (var m in new[] { troisieme, premier, deuxieme })
            await file.EnqueueAsync(m);

        var ordre = (await file.GetPendingAsync()).Select(m => m.ClientGuid);

        ordre.Should().Equal(premier.ClientGuid, deuxieme.ClientGuid, troisieme.ClientGuid);
    }

    [Fact]
    public async Task EtantDonneMouvementConfirme_QuandLireLaFile_AlorsIlNEnFaitPlusPartie()
    {
        var file = new SqliteLocalMovementQueue(_chemin);
        var confirme = Mouvement(0);
        var enAttente = Mouvement(1);
        await file.EnqueueAsync(confirme);
        await file.EnqueueAsync(enAttente);

        await file.MarkSyncedAsync(confirme.ClientGuid);

        (await file.GetPendingCountAsync()).Should().Be(1);
        (await file.GetPendingAsync()).Single().ClientGuid.Should().Be(enAttente.ClientGuid);
    }

    [Fact]
    public async Task EtantDonneIdentifiantInconnu_QuandMarquerSynchronise_AlorsSansEffetNiErreur()
    {
        var file = new SqliteLocalMovementQueue(_chemin);
        await file.EnqueueAsync(Mouvement(0));

        var act = () => file.MarkSyncedAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
        (await file.GetPendingCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EtantDonneApplicationRedemarree_QuandRouvrirLaFile_AlorsMouvementsEnAttenteRetrouves()
    {
        var mouvement = Mouvement(0);
        await new SqliteLocalMovementQueue(_chemin).EnqueueAsync(mouvement);

        var rouverte = new SqliteLocalMovementQueue(_chemin);

        (await rouverte.GetPendingAsync()).Single().ClientGuid.Should().Be(mouvement.ClientGuid);
    }

    [Fact]
    public async Task EtantDonneMemeIdentifiantClient_QuandMettreEnFileDeuxFois_AlorsRefuse()
    {
        // Le ClientGuid sert de clé d'idempotence côté serveur : il ne peut pas exister en double.
        var file = new SqliteLocalMovementQueue(_chemin);
        var mouvement = Mouvement(0);
        await file.EnqueueAsync(mouvement);

        var act = () => file.EnqueueAsync(new LocalMovement { ClientGuid = mouvement.ClientGuid, Quantity = 1 });

        await act.Should().ThrowAsync<SQLiteException>();
        (await file.GetPendingCountAsync()).Should().Be(1);
    }
}
