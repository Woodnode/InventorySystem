using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Sync;
using InventoryMobile.Services;

namespace InventoryMobile.ViewModels;

/// <summary>
/// Ligne du catalogue, comme la liste mobile du web : titre et SKU, étiquettes de classement,
/// emplacement principal, quantité et badge de stock bas.
/// </summary>
public sealed class ProductRowItem(ProductResponse product)
{
    public ProductResponse Product { get; } = product;

    public string Name => Product.Name;

    public string Sku => Product.Sku;

    public IReadOnlyList<string> Tags { get; } =
        [.. new[] { product.Collection, product.ProductType, product.Year?.ToString() }.OfType<string>().Where(t => t.Length > 0)];

    public bool HasTags => Tags.Count > 0;

    public string? Location => Product.PrimaryLocation;

    public bool HasLocation => !string.IsNullOrWhiteSpace(Product.PrimaryLocation);

    public string QuantityLabel => $"{FrenchFormat.Number(Product.Quantity)} restant{(Product.Quantity > 1 ? "s" : "")}";

    public bool IsLowOnStock => Product.IsLowOnStock;
}

/// <summary>
/// Catalogue paginé côté serveur, comme le web : recherche par SKU ou titre, filtre « stock
/// bas », vingt produits par page chargés au fil du défilement. L'ancienne version parcourait
/// tout le catalogue (jusqu'à cinquante pages de cent) à chaque affichage. Rejoue aussi la file
/// d'attente des mouvements hors ligne avant de charger.
/// </summary>
public sealed partial class ProductsViewModel : ObservableObject, IQueryAttributable
{
    private const int PageSize = 20;

    private readonly IInventoryApi _api;
    private readonly IMovementSyncService _syncService;
    private readonly INavigationService _navigation;
    private CancellationTokenSource? _searchDebounce;
    private int _loadedPage;
    private bool _initialized;

    public ProductsViewModel(IInventoryApi api, IMovementSyncService syncService, INavigationService navigation)
    {
        _api = api;
        _syncService = syncService;
        _navigation = navigation;
        Filters = OptionChoice.Single(
            [(false, "Tous les produits"), (true, "Stock bas")], false, lowOnly => LowStockOnly = lowOnly);
    }

    public ObservableCollection<ProductRowItem> Products { get; } = [];

    public IReadOnlyList<OptionChoice> Filters { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _lowStockOnly;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pendingSyncCount;

    public string CountLabel => FrenchFormat.Count(TotalCount, "produit", "produits");

    public bool HasMore => Products.Count < TotalCount;

    public string EmptyMessage => string.IsNullOrWhiteSpace(SearchText)
        ? LowStockOnly ? "Aucun produit sous le seuil." : "Aucun produit au catalogue."
        : $"Aucun produit ne correspond à « {SearchText.Trim()} ».";

    public string PendingSyncLabel => PendingSyncCount > 1
        ? $"{PendingSyncCount} mouvements attendent le réseau pour être envoyés."
        : "1 mouvement attend le réseau pour être envoyé.";

    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(CountLabel));

    partial void OnPendingSyncCountChanged(int value) => OnPropertyChanged(nameof(PendingSyncLabel));

    partial void OnLowStockOnlyChanged(bool value)
    {
        OptionChoice.SelectValue(Filters, value);
        if (_initialized) _ = LoadAsync();
    }

    // Recherche débouncée comme sur le web : une requête après 300 ms sans frappe, pas une par lettre.
    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce?.Cancel();
        var debounce = _searchDebounce = new CancellationTokenSource();
        _ = Task.Delay(300, debounce.Token).ContinueWith(
            _ => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync()),
            debounce.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    /// <summary>Le tableau de bord ouvre la liste déjà filtrée sur le stock bas.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("LowStockOnly", out var value) && value is bool lowOnly)
        {
            LowStockOnly = lowOnly;
        }
    }

    public async Task InitializeAsync()
    {
        // Rejouer la file d'attente AVANT de charger les produits : les quantités affichées
        // doivent déjà refléter les mouvements hors ligne synchronisés.
        try
        {
            PendingSyncCount = (await _syncService.SyncPendingAsync()).RemainingPendingCount;
        }
        catch (Exception)
        {
            // Best-effort : le prochain passage sur cette page réessaiera.
        }

        _initialized = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var response = await FetchPageAsync(1);
            Products.Clear();
            foreach (var product in response.Items)
            {
                Products.Add(new ProductRowItem(product));
            }

            _loadedPage = 1;
            TotalCount = response.TotalCount;
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de charger les produits. Vérifiez votre connexion.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasMore));
            OnPropertyChanged(nameof(EmptyMessage));
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || IsLoadingMore || !HasMore) return;

        try
        {
            IsLoadingMore = true;
            var response = await FetchPageAsync(_loadedPage + 1);
            foreach (var product in response.Items)
            {
                Products.Add(new ProductRowItem(product));
            }

            _loadedPage++;
            TotalCount = response.TotalCount;
        }
        catch (Exception)
        {
            // La page suivante sera redemandée au prochain défilement.
        }
        finally
        {
            IsLoadingMore = false;
            OnPropertyChanged(nameof(HasMore));
        }
    }

    private Task<PagedResponse<ProductResponse>> FetchPageAsync(int page) =>
        _api.GetProductsAsync(
            page, PageSize,
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            LowStockOnly ? true : null);

    [RelayCommand]
    private async Task SelectProductAsync(ProductRowItem? item)
    {
        if (item is null) return;

        await _navigation.GoToAsync("product", new Dictionary<string, object> { ["Product"] = item.Product });
    }

    [RelayCommand]
    private Task ScanAsync() => _navigation.GoToAsync("scan");
}
