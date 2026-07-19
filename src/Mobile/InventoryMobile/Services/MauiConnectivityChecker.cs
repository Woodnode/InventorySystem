using InventoryMobile.Application.Connectivity;

namespace InventoryMobile.Services;

/// <summary>
/// Implémentation de <see cref="IConnectivityChecker"/> adossée à <c>Connectivity.Current</c>
/// (Microsoft.Maui.Networking). Vit dans le head MAUI, comme <see cref="SecureStorageTokenStore"/> :
/// seul ce projet référence Microsoft.Maui.Essentials.
/// </summary>
public sealed class MauiConnectivityChecker : IConnectivityChecker
{
    public bool IsConnected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
}
