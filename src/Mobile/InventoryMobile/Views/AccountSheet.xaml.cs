using InventoryMobile.Application.Auth;
using InventoryMobile.Resources;
using InventoryMobile.Services;

namespace InventoryMobile.Views;

/// <summary>
/// Feuille du compte : identité, thème et déconnexion. Elle monte du bas en 220 ms et le fond
/// s'assombrit, ce qui la rattache visuellement à l'écran d'où elle vient.
/// </summary>
public partial class AccountSheet : ContentPage
{
    private const uint Duration = 220;

    private readonly AccountActions _actions;
    private bool _closing;

    public AccountSheet(AccountActions actions, AuthSession? session)
    {
        InitializeComponent();
        _actions = actions;

        var (name, role) = SplitRole(session?.DisplayName ?? "Compte");
        NameLabel.Text = name;
        EmailLabel.Text = session?.Email;
        EmailLabel.IsVisible = session is not null;
        RoleLabel.Text = role;
        RolePill.IsVisible = role is not null;
        InitialsLabel.Text = Initials(name);

        DarkSwitch.IsToggled = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
        ThemeIcon.Text = DarkSwitch.IsToggled ? Icons.Moon : Icons.Sun;
    }

    /// <summary>« Gaby Demo (Gestionnaire) » : le rôle, entre parenthèses, passe dans sa pastille.</summary>
    private static (string Name, string? Role) SplitRole(string displayName)
    {
        var open = displayName.LastIndexOf('(');
        return open > 0 && displayName.EndsWith(')')
            ? (displayName[..open].Trim(), displayName[(open + 1)..^1].Trim())
            : (displayName, null);
    }

    private static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Sheet.TranslationY = Math.Max(Sheet.Height, 640);
        await Task.WhenAll(
            Scrim.FadeToAsync(0.45, Duration, Easing.CubicOut),
            Sheet.TranslateToAsync(0, 0, Duration, Easing.CubicOut));
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private async void OnCloseRequested(object? sender, EventArgs e) => await CloseAsync();

    private void OnDarkToggled(object? sender, ToggledEventArgs e)
    {
        _actions.SetDarkTheme(e.Value);
        ThemeIcon.Text = e.Value ? Icons.Moon : Icons.Sun;
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await CloseAsync();
        await _actions.LogoutAsync();
    }

    private async Task CloseAsync()
    {
        if (_closing) return;
        _closing = true;

        await Task.WhenAll(
            Scrim.FadeToAsync(0, Duration, Easing.CubicIn),
            Sheet.TranslateToAsync(0, Sheet.Height, Duration, Easing.CubicIn));
        await Navigation.PopModalAsync(false);
    }
}
