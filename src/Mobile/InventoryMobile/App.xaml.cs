using Microsoft.Extensions.DependencyInjection;

namespace InventoryMobile;

// Qualification complète nécessaire : "InventoryMobile.Application" (la couche Clean
// Architecture) est un espace de noms visible sans "using" depuis "InventoryMobile" et
// masque le type Microsoft.Maui.Controls.Application pour un nom simple "Application".
public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // AppShell résolu via DI (pas "new AppShell()") : ses pages injectent leur
        // ViewModel par constructeur, il faut donc que la résolution passe par le
        // conteneur de bout en bout (plan §8.4).
        var shell = _services.GetRequiredService<AppShell>();
        return new Window(shell);
    }
}
