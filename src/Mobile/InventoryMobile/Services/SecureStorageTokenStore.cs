using System.Text.Json;
using InventoryMobile.Application.Auth;

namespace InventoryMobile.Services;

/// <summary>
/// Implémentation de <see cref="ITokenStore"/> adossée à <c>SecureStorage</c> (Keychain /
/// Keystore / Credential Locker selon la plateforme — chiffré par l'OS). Vit dans le head
/// MAUI, seul projet référençant <c>Microsoft.Maui.Essentials</c> ; Infrastructure ne
/// connaît que l'abstraction <see cref="ITokenStore"/> (Dependency Inversion, plan §7).
/// </summary>
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string StorageKey = "auth_session";

    public async Task SaveAsync(AuthSession session)
    {
        var json = JsonSerializer.Serialize(session);
        await SecureStorage.Default.SetAsync(StorageKey, json);
    }

    public async Task<AuthSession?> LoadAsync()
    {
        var json = await SecureStorage.Default.GetAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<AuthSession>(json);
        }
        catch (JsonException)
        {
            // Format corrompu/obsolète (ex. après un changement de schéma) : traiter comme
            // "pas de session" plutôt que planter au démarrage de l'app.
            SecureStorage.Default.Remove(StorageKey);
            return null;
        }
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
