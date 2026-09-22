using InventoryMobile.Views;

namespace InventoryMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // "login" et "products" sont déclarés en ShellContent (navigation absolue //).
        // "movement" et "scan" sont poussés sur la pile via RegisterRoute.
        Routing.RegisterRoute("movement", typeof(MovementPage));
        Routing.RegisterRoute("scan", typeof(ScanPage));
    }
}
