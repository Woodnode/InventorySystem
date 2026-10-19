using InventoryMobile.Application.Navigation;
using InventoryMobile.Services;

namespace InventoryMobile.Controls;

public partial class AppHeader : ContentView
{
    public AppHeader()
    {
        InitializeComponent();
    }

    // L'en-tête est posé dans le XAML de chaque onglet, hors injection par constructeur : ses
    // services sont lus dans le conteneur de l'application.
    private static T Resolve<T>() where T : notnull =>
        IPlatformApplication.Current!.Services.GetRequiredService<T>();

    private async void OnScanClicked(object? sender, EventArgs e) =>
        await Resolve<INavigationService>().GoToAsync("scan");

    private async void OnAccountClicked(object? sender, EventArgs e)
    {
        if (this.FindAncestor<Page>() is { } page)
        {
            await Resolve<AccountActions>().ShowMenuAsync(page);
        }
    }
}

internal static class ElementExtensions
{
    public static T? FindAncestor<T>(this Element element) where T : Element
    {
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is T match)
            {
                return match;
            }
        }

        return null;
    }
}
