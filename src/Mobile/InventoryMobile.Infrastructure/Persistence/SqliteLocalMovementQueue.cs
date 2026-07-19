using SQLite;

namespace InventoryMobile.Infrastructure.Persistence;

/// <summary>
/// Implémentation SQLite de <see cref="ILocalMovementQueue"/> (sqlite-net-pcl). La table est
/// créée paresseusement (le constructeur reste synchrone) ; la tâche d'initialisation est
/// mise en cache pour n'exécuter le CREATE TABLE qu'une seule fois pour la durée de vie du
/// singleton.
/// </summary>
public sealed class SqliteLocalMovementQueue : ILocalMovementQueue
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly Task _initialization;

    public SqliteLocalMovementQueue(string databasePath)
    {
        _connection = new SQLiteAsyncConnection(databasePath);
        _initialization = _connection.CreateTableAsync<LocalMovement>();
    }

    public async Task EnqueueAsync(LocalMovement movement)
    {
        await _initialization;
        await _connection.InsertAsync(movement);
    }

    public async Task<IReadOnlyList<LocalMovement>> GetPendingAsync()
    {
        await _initialization;
        return await _connection.Table<LocalMovement>()
            .Where(m => !m.IsSynced)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task MarkSyncedAsync(Guid clientGuid)
    {
        await _initialization;
        var movement = await _connection.Table<LocalMovement>()
            .Where(m => m.ClientGuid == clientGuid)
            .FirstOrDefaultAsync();
        if (movement is null) return;

        movement.IsSynced = true;
        await _connection.UpdateAsync(movement);
    }

    public async Task<int> GetPendingCountAsync()
    {
        await _initialization;
        return await _connection.Table<LocalMovement>()
            .Where(m => !m.IsSynced)
            .CountAsync();
    }
}
