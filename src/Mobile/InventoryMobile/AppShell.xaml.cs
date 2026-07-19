using InventoryMobile.Views;

namespace InventoryMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // "login" est déclarée directement dans le XAML (ShellContent) ; "products",
        // "movement" et "scan" ne sont atteintes que par navigation programmatique
        // (GoToAsync) — elles doivent donc être enregistrées explicitement (plan §8.4).
        Routing.RegisterRoute("products", typeof(ProductsPage));
        Routing.RegisterRoute("movement", typeof(MovementPage));
        Routing.RegisterRoute("scan", typeof(ScanPage));
    }
}
