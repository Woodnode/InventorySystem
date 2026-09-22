using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Application.Notifications;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Sync;

namespace InventoryMobile.ViewModels;

/// <summary>
/// Liste des produits avec stock total agrégé (miroir de ProductsPage côté web). Rejoue
/// aussi la file d'attente de mouvements offline à chaque affichage (voir
/// <see cref="SyncPendingMovementsAsync"/>). L'écoute des alertes de stock bas temps réel
/// vit désormais au niveau de la session (voir <see cref="IStockAlertSessionListener"/> et
/// AUDIT.md M-4), pas de cet écran — ce ViewModel n'a plus besoin de connaître SignalR.
/// </summary>
public sealed partial class ProductsViewModel : ObservableObject
{
    private readonly IInventoryApi _api;
    private readonly IAuthService _authService;
    private readonly IMovementSyncService _syncService;
    private readonly IStockAlertSessionListener _stockAlertSessionListener;
    private readonly INavigationService _navigation;

    public ProductsViewModel(
        IInventoryApi api,
        IAuthService authService,
        IMovementSyncService syncService,
        IStockAlertSessionListener stockAlertSessionListener,
        INavigationService navigation)
    {
        _api = api;
        _authService = authService;
        _syncService = syncService;
        _stockAlertSessionListener = stockAlertSessionListener;
        _navigation = navigation;
    }

    public ObservableCollection<ProductResponse> Products { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _displayName;

    [ObservableProperty]
    private int _pendingSyncCount;

    /// <summary>
    /// Nombre de produits sous leur seuil de réappro, lu depuis l'endpoint dédié
    /// GET /products/low-stock — jusqu'ici jamais appelé côté mobile (voir AUDIT.md M-1),
    /// alors que le catalogue complet était parcouru en boucle rien que pour cette info.
    /// </summary>
    [ObservableProperty]
    private int _lowStockCount;

    public async Task InitializeAsync()
    {
        DisplayName = _authService.CurrentSession?.DisplayName;

        // Rejouer la file d'attente AVANT de charger les produits : si des mouvements
        // offline sont synchronisés avec succès, les quantités affichées doivent déjà
        // refléter les mises à jour de stock qu'ils ont provoquées côté serveur.
        await SyncPendingMovementsAsync();
        await LoadAsync();
    }

    private async Task SyncPendingMovementsAsync()
    {
        try
        {
            var result = await _syncService.SyncPendingAsync();
            PendingSyncCount = result.RemainingPendingCount;
        }
        catch (Exception)
        {
            // Best-effort : une synchronisation ratée ne doit jamais empêcher l'affichage
            // des produits. Le prochain passage sur cette page réessaiera.
        }
    }

    private async Task LoadLowStockCountAsync()
    {
        try
        {
            // pageSize=1 : seul TotalCount nous intéresse ici, pas la page d'items.
            var response = await _api.GetLowStockAsync(page: 1, pageSize: 1);
            LowStockCount = response.TotalCount;
        }
        catch (Exception)
        {
            // Best-effort, comme le reste de LoadAsync : ne bloque jamais l'affichage du
            // catalogue pour un badge secondaire.
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            // Parcourt les pages backend (pageSize max 100) pour afficher tout le catalogue.
            const int pageSize = 100;
            const int maxPages = 50;
            var all = new List<ProductResponse>();
            var page = 1;
            var totalCount = int.MaxValue;

            while (page <= maxPages && all.Count < totalCount)
            {
                var response = await _api.GetProductsAsync(page, pageSize);
                totalCount = response.TotalCount;
                all.AddRange(response.Items);
                if (response.Items.Count == 0) break;
                page++;
            }

            Products.Clear();
            foreach (var product in all.OrderBy(p => p.Name))
            {
                Products.Add(product);
            }

            await LoadLowStockCountAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de charger les produits. Vérifiez votre connexion.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectProductAsync(ProductResponse? product)
    {
        if (product is null) return;

        await _navigation.GoToAsync(
            "movement",
            new Dictionary<string, object> { ["Product"] = product });
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        // Une session déconnectée ne doit plus recevoir d'alertes ni garder une connexion
        // authentifiée avec un token qui va être effacé.
        await _stockAlertSessionListener.StopAsync();
        await _authService.LogoutAsync();
        await _navigation.GoToAsync("//login");
    }

    [RelayCommand]
    private async Task ScanAsync() => await _navigation.GoToAsync("scan");
}
