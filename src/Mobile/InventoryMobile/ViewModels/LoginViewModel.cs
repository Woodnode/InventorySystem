using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Application.Notifications;
using InventoryMobile.Services;
using Refit;

namespace InventoryMobile.ViewModels;

/// <summary>ViewModel de connexion (plan §8.4, jalon 1 : auth en ligne).</summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigation;
    private readonly IStockAlertSessionListener _stockAlertSessionListener;

    public LoginViewModel(
        IAuthService authService,
        INavigationService navigation,
        IStockAlertSessionListener stockAlertSessionListener)
    {
        _authService = authService;
        _navigation = navigation;
        _stockAlertSessionListener = stockAlertSessionListener;
    }

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Restaure une session déjà persistée ; navigue vers les produits si OK.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (await _authService.RestoreSessionAsync() is not null)
            {
                // Voir AUDIT.md M-4 : démarré ici (session établie), pas dans
                // ProductsPage.OnAppearing — les alertes de stock bas doivent arriver pour
                // toute la session, pas seulement pendant que cet écran précis est affiché.
                await _stockAlertSessionListener.StartAsync();
                await _navigation.GoToAsync("//products");
            }
        }
        catch (Exception)
        {
            // Appelée depuis OnAppearing (async void, voir LoginPage.xaml.cs) : un échec de
            // lecture SecureStorage (Keystore Android indisponible, etc.) ne doit jamais faire
            // planter l'app au lancement — au pire l'utilisateur retape ses identifiants
            // (voir ré-audit, incohérence avec ProductsViewModel/MovementViewModel qui se
            // protègent déjà de la même façon).
            ErrorMessage = "Impossible de restaurer la session. Reconnecte-toi.";
        }
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

            await _stockAlertSessionListener.StartAsync();
            await _navigation.GoToAsync("//products");
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
            || ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
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
