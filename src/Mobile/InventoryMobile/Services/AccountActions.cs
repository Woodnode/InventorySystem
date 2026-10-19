using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Application.Notifications;

namespace InventoryMobile.Services;

/// <summary>
/// Menu du compte, l'équivalent du menu mobile du web : identité et rôle, bascule du thème,
/// déconnexion. Partagé par l'en-tête de tous les onglets.
/// </summary>
public sealed class AccountActions(
    IAuthService authService,
    INavigationService navigation,
    IStockAlertSessionListener stockAlertSessionListener)
{
    private const string ThemeKey = "inventaire-theme";

    /// <summary>Réapplique le thème choisi au lancement (clair, sombre, ou celui du système).</summary>
    public static void RestoreTheme()
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null)
        {
            return;
        }

        app.UserAppTheme = Preferences.Default.Get(ThemeKey, string.Empty) switch
        {
            "dark" => AppTheme.Dark,
            "light" => AppTheme.Light,
            _ => AppTheme.Unspecified,
        };
    }

    /// <summary>Ouvre la feuille du compte par-dessus la page courante.</summary>
    public Task ShowMenuAsync(Page page) =>
        page.Navigation.PushModalAsync(new Views.AccountSheet(this, authService.CurrentSession), animated: false);

    public void SetDarkTheme(bool dark)
    {
        if (Microsoft.Maui.Controls.Application.Current is not { } app) return;

        app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
        Preferences.Default.Set(ThemeKey, dark ? "dark" : "light");
    }

    public async Task LogoutAsync()
    {
        // Une session déconnectée ne doit plus recevoir d'alertes ni garder une connexion
        // authentifiée avec un token qui va être effacé.
        await stockAlertSessionListener.StopAsync();
        await authService.LogoutAsync();
        await navigation.GoToAsync("//login");
    }
}
