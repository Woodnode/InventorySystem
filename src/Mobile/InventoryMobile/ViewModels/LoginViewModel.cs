using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Application.Auth;
using InventoryMobile.Services;
using Refit;

namespace InventoryMobile.ViewModels;

/// <summary>ViewModel de connexion (plan §8.4, jalon 1 : auth en ligne).</summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Restaure une session déjà persistée (SecureStorage) au démarrage de l'app.</summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        var session = await _authService.RestoreSessionAsync();
        return session is not null;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Courriel et mot de passe requis.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            await _authService.LoginAsync(Email.Trim(), Password);
            Password = string.Empty;

            await Shell.Current.GoToAsync("//products");
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Message volontairement générique côté backend (anti-énumération de comptes) —
            // on l'affiche tel quel.
            ErrorMessage = await ApiErrorReader.ReadDetailAsync(ex) ?? "Identifiants invalides.";
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de joindre le serveur. Vérifiez votre connexion.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
