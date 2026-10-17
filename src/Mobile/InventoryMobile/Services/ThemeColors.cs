namespace InventoryMobile.Services;

/// <summary>
/// Lit un jeton de Colors.xaml dans le thème courant, pour les couleurs calculées dans un
/// ViewModel (remplissage d'une jauge selon l'urgence). Les vues passent par AppThemeBinding.
/// </summary>
public static class ThemeColors
{
    public static Color Get(string token)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        var suffix = app?.RequestedTheme == AppTheme.Dark ? "Dark" : "Light";
        if (app?.Resources.TryGetValue(token + suffix, out var themed) == true && themed is Color color)
        {
            return color;
        }

        return app?.Resources.TryGetValue(token, out var plain) == true && plain is Color single ? single : Colors.Gray;
    }
}
