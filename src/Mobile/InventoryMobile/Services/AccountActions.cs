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

    public async Task ShowMenuAsync(Page page)
    {
        var session = authService.CurrentSession;
        var title = session is null
            ? "Compte"
            // Le nom affiché porte déjà le rôle (« Émile Demo (Employé) ») : on y ajoute l'adresse.
            : $"{session.DisplayName}\n{session.Email}";

        var app = Microsoft.Maui.Controls.Application.Current!;
        var isDark = app.RequestedTheme == AppTheme.Dark;
        var themeLabel = isDark ? "Activer le thème clair" : "Activer le thème sombre";

        var choice = await page.DisplayActionSheetAsync(title, "Annuler", "Déconnexion", themeLabel);
        if (choice == themeLabel)
        {
            app.UserAppTheme = isDark ? AppTheme.Light : AppTheme.Dark;
            Preferences.Default.Set(ThemeKey, isDark ? "light" : "dark");
        }
        else if (choice == "Déconnexion")
        {
            await LogoutAsync();
        }
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
