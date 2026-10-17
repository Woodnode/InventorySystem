using InventoryMobile.Views;

namespace InventoryMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // "login" et les onglets sont déclarés dans AppShell.xaml (navigation absolue //).
        // La fiche produit et le scan sont poussés sur la pile de l'onglet courant.
        Routing.RegisterRoute("product", typeof(ProductDetailPage));
        Routing.RegisterRoute("scan", typeof(ScanPage));
    }
}
