using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Notifications;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Realtime;
using InventoryMobile.Infrastructure.Sync;

namespace InventoryMobile.ViewModels;

/// <summary>
/// Liste des produits avec stock total agrégé (miroir de ProductsPage côté web). Rejoue
/// aussi la file d'attente de mouvements offline à chaque affichage (voir
/// <see cref="SyncPendingMovementsAsync"/>) et écoute les alertes de stock bas temps réel
/// (voir <see cref="EnsureStockAlertListenerStartedAsync"/>).
/// </summary>
public sealed partial class ProductsViewModel : ObservableObject
{
    private readonly IInventoryApi _api;
    private readonly IAuthService _authService;
    private readonly IMovementSyncService _syncService;
    private readonly IStockAlertHubClient _stockAlertHubClient;
    private readonly ILocalNotificationService _notificationService;

    public ProductsViewModel(
        IInventoryApi api,
        IAuthService authService,
        IMovementSyncService syncService,
        IStockAlertHubClient stockAlertHubClient,
        ILocalNotificationService notificationService)
    {
        _api = api;
        _authService = authService;
        _syncService = syncService;
        _stockAlertHubClient = stockAlertHubClient;
        _notificationService = notificationService;
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

    public async Task InitializeAsync()
    {
        DisplayName = _authService.CurrentSession?.DisplayName;

        // Rejouer la file d'attente AVANT de charger les produits : si des mouvements
        // offline sont synchronisés avec succès, les quantités affichées doivent déjà
        // refléter les mises à jour de stock qu'ils ont provoquées côté serveur.
        await SyncPendingMovementsAsync();
        await EnsureStockAlertListenerStartedAsync();
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

    private async Task EnsureStockAlertListenerStartedAsync()
    {
        // -= puis += : rend l'abonnement idempotent quel que soit le nombre de fois où
        // InitializeAsync est appelé (OnAppearing se déclenche à chaque retour sur la page).
        _stockAlertHubClient.LowStockAlertReceived -= OnLowStockAlertReceived;
        _stockAlertHubClient.LowStockAlertReceived += OnLowStockAlertReceived;

        try
        {
            await _stockAlertHubClient.StartAsync();
        }
        catch (Exception)
        {
            // Best-effort, comme la sync offline : une connexion SignalR ratée ne doit jamais
            // empêcher l'affichage des produits. WithAutomaticReconnect() gère les coupures
            // transitoires ; sinon retentera au prochain passage sur cette page.
        }
    }

    private async void OnLowStockAlertReceived(object? sender, LowStockAlert alert) =>
        await _notificationService.ShowLowStockAlertAsync(alert);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var response = await _api.GetProductsAsync();

            Products.Clear();
            foreach (var product in response.Items.OrderBy(p => p.Name))
            {
                Products.Add(product);
            }
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

        await Shell.Current.GoToAsync(
            "movement",
            new Dictionary<string, object> { ["Product"] = product });
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        // Une session déconnectée ne doit plus recevoir d'alertes ni garder une connexion
        // authentifiée avec un token qui va être effacé.
        await _stockAlertHubClient.StopAsync();
        await _authService.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }

    [RelayCommand]
    private async Task ScanAsync() => await Shell.Current.GoToAsync("scan");
}
