namespace InventoryMobile.Application.Connectivity;

/// <summary>
/// Abstraction de l'état de connectivité réseau (implémentée par le head MAUI via
/// <c>Connectivity.Current</c> — même partition que <see cref="Auth.ITokenStore"/> :
/// seul le head project référence Microsoft.Maui.Essentials, l'Infrastructure ne
/// connaît que ce contrat).
/// </summary>
public interface IConnectivityChecker
{
    /// <summary>Vrai si l'appareil dispose d'un accès réseau vers Internet au moment de l'appel.</summary>
    bool IsConnected { get; }
}
