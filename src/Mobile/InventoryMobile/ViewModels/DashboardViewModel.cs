using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Sync;
using InventoryMobile.Services;

namespace InventoryMobile.ViewModels;

/// <summary>
/// Produit sous son seuil, avec la barre stock / seuil du web : un « 7 restants » seul obligeait
/// à chercher le seuil ailleurs pour juger de l'urgence.
/// </summary>
public sealed class LowStockItem(ProductResponse product)
{
    public ProductResponse Product { get; } = product;

    public string Name => Product.Name;

    public double Ratio => Product.LowStockThreshold > 0 ? Math.Min(1, (double)Product.Quantity / Product.LowStockThreshold) : 1;

    /// <summary>Rupture et tiers du seuil en rose, le reste en ambre (BarreStock du web).</summary>
    public Color FillColor => ThemeColors.Get(Product.Quantity == 0 || Ratio < 0.34 ? "Danger" : "Warning");

    public string QuantityLabel => FrenchFormat.Number(Product.Quantity);

    public string ThresholdLabel => $" / {FrenchFormat.Number(Product.LowStockThreshold)}";

    public string Description => $"{Product.Name}, {Product.Quantity} en stock sur un seuil de {Product.LowStockThreshold}";
}

/// <summary>Ligne d'une répartition (entrepôts, collections) : libellé, valeur, part du maximum.</summary>
public sealed record ShareItem(string Name, string ValueLabel, double Ratio, string? Caption = null);

/// <summary>
/// Tableau de bord du web en largeur téléphone : produits en stock bas d'abord (c'est ce qui
/// demande une action), puis les indicateurs, le flux de stock et les répartitions.
/// </summary>
public sealed partial class DashboardViewModel(
    IInventoryApi api, IMovementSyncService syncService, INavigationService navigation) : ObservableObject
{
    /// <summary>Nombre de produits critiques repris sur l'accueil, comme le web.</summary>
    private const int LowStockPreview = 6;

    public ObservableCollection<LowStockItem> LowStock { get; } = [];

    public ObservableCollection<ShareItem> WarehouseLoads { get; } = [];

    public ObservableCollection<ShareItem> Collections { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _pendingSyncCount;

    [ObservableProperty]
    private int _lowStockTotal;

    [ObservableProperty]
    private DashboardStatsResponse? _stats;

    [ObservableProperty]
    private IReadOnlyList<int> _flowValues = [];

    /// <summary>Squelette du premier chargement, retiré si l'appel échoue (l'erreur prend sa place).</summary>
    public bool ShowSkeleton => !IsLoaded && string.IsNullOrEmpty(ErrorMessage);

    partial void OnIsLoadedChanged(bool value) => OnPropertyChanged(nameof(ShowSkeleton));

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(ShowSkeleton));

    public bool HasLowStock => LowStock.Count > 0;

    public bool HasMoreLowStock => LowStockTotal > LowStock.Count;

    public string LowStockTotalLabel => $"{FrenchFormat.Number(LowStockTotal)} au total";

    public string SeeAllLowStockLabel => $"Voir les {FrenchFormat.Number(LowStockTotal)} produits sous le seuil";

    public string PendingSyncLabel => PendingSyncCount > 1
        ? $"{PendingSyncCount} mouvements attendent le réseau pour être envoyés."
        : "1 mouvement attend le réseau pour être envoyé.";

    public string TotalProductsLabel => Stats is null ? "-" : FrenchFormat.Number(Stats.TotalProducts);

    public string ActiveWarehousesLabel => Stats is null ? "-" : FrenchFormat.Number(Stats.ActiveWarehouses);

    public string? InactiveWarehousesLabel => Stats is { InactiveWarehouses: > 0 } s
        ? FrenchFormat.Count(s.InactiveWarehouses, "inactif", "inactifs")
        : null;

    public string TotalWeightLabel => Stats is null ? "-" : FrenchFormat.Weight(Stats.TotalWeightGrams);

    public string FlowInLabel => FrenchFormat.Number(Stats?.DailyFlow.Sum(f => f.In) ?? 0);

    public string FlowOutLabel => FrenchFormat.Number(Stats?.DailyFlow.Sum(f => f.Out) ?? 0);

    public string FlowNetLabel => Stats is null ? "-" : (Stats.NetCurrentPeriod > 0 ? "+" : "") + FrenchFormat.Number(Stats.NetCurrentPeriod);

    public string FlowDescription => Stats is null ? string.Empty
        : $"Solde quotidien sur {Stats.PeriodDays} jours : {FlowInLabel} unités entrées, {FlowOutLabel} sorties, solde net de {FlowNetLabel}.";

    /// <summary>
    /// Variation entre deux périodes, volontairement neutre : un stock qui baisse peut traduire
    /// des ventes. La couleur dit le sens, pas un jugement (Variation du web).
    /// </summary>
    public string VariationLabel => Stats?.ChangePercent switch
    {
        null => "Pas de comparaison possible",
        var p => $"{(p > 0 ? "+" : "")}{p.Value.ToString("0.#", FrenchFormat.Culture)} % sur {Stats.PeriodDays} j",
    };

    public string VariationIcon => Stats?.ChangePercent switch
    {
        null or 0 => Resources.Icons.Minus,
        > 0 => Resources.Icons.TrendUp,
        _ => Resources.Icons.TrendDown,
    };

    public bool VariationIsUp => Stats?.ChangePercent > 0;

    public bool VariationIsDown => Stats?.ChangePercent < 0;

    public bool VariationIsFlat => !VariationIsUp && !VariationIsDown;

    partial void OnPendingSyncCountChanged(int value) => OnPropertyChanged(nameof(PendingSyncLabel));

    partial void OnLowStockTotalChanged(int value)
    {
        OnPropertyChanged(nameof(LowStockTotalLabel));
        OnPropertyChanged(nameof(SeeAllLowStockLabel));
        OnPropertyChanged(nameof(HasMoreLowStock));
    }

    partial void OnStatsChanged(DashboardStatsResponse? value)
    {
        foreach (var name in new[]
                 {
                     nameof(TotalProductsLabel), nameof(ActiveWarehousesLabel), nameof(InactiveWarehousesLabel),
                     nameof(TotalWeightLabel), nameof(FlowInLabel), nameof(FlowOutLabel), nameof(FlowNetLabel),
                     nameof(FlowDescription), nameof(VariationLabel), nameof(VariationIcon), nameof(VariationIsUp),
                     nameof(VariationIsDown), nameof(VariationIsFlat),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    private bool _followsTheme;

    public async Task InitializeAsync()
    {
        // La teinte des jauges est calculée ici (rose ou ambre selon l'urgence) : un changement de
        // thème depuis le menu du compte doit la recalculer sans attendre un rechargement.
        if (!_followsTheme && Microsoft.Maui.Controls.Application.Current is { } app)
        {
            _followsTheme = true;
            app.RequestedThemeChanged += (_, _) =>
            {
                var products = LowStock.Select(i => i.Product).ToList();
                LowStock.Clear();
                foreach (var product in products) LowStock.Add(new LowStockItem(product));
            };
        }

        try
        {
            PendingSyncCount = (await syncService.SyncPendingAsync()).RemainingPendingCount;
        }
        catch (Exception)
        {
            // Best-effort : une synchronisation ratée n'empêche pas l'affichage.
        }

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

            var lowStockTask = api.GetLowStockAsync(page: 1, pageSize: LowStockPreview);
            var statsTask = api.GetDashboardStatsAsync();
            await Task.WhenAll(lowStockTask, statsTask);

            // Tri par criticité (quantité rapportée au seuil) : une rupture passe devant un
            // produit à 90 % de son seuil.
            var lowStock = lowStockTask.Result;
            LowStock.Clear();
            foreach (var product in lowStock.Items
                         .OrderBy(p => p.LowStockThreshold > 0 ? (double)p.Quantity / p.LowStockThreshold : 1))
            {
                LowStock.Add(new LowStockItem(product));
            }
            LowStockTotal = lowStock.TotalCount;
            OnPropertyChanged(nameof(HasLowStock));
            OnPropertyChanged(nameof(HasMoreLowStock));

            var stats = statsTask.Result;
            Stats = stats;
            FlowValues = [.. stats.DailyFlow.Select(f => f.Net)];

            WarehouseLoads.Clear();
            var maxLoad = Math.Max(1, stats.WarehouseLoads.Select(l => l.TotalQuantity).DefaultIfEmpty(0).Max());
            var totalLoad = stats.WarehouseLoads.Sum(l => l.TotalQuantity);
            foreach (var load in stats.WarehouseLoads)
            {
                var share = totalLoad > 0 ? $", {Math.Round(100.0 * load.TotalQuantity / totalLoad)} % du stock" : string.Empty;
                WarehouseLoads.Add(new ShareItem(
                    load.Name,
                    $"{FrenchFormat.Number(load.TotalQuantity)} unités",
                    (double)load.TotalQuantity / maxLoad,
                    FrenchFormat.Count(load.ProductCount, "référence", "références") + share));
            }

            Collections.Clear();
            var maxCollection = Math.Max(1, stats.TopCollections.Select(c => c.Count).DefaultIfEmpty(0).Max());
            foreach (var collection in stats.TopCollections)
            {
                Collections.Add(new ShareItem(collection.Collection, FrenchFormat.Number(collection.Count),
                    (double)collection.Count / maxCollection));
            }

            IsLoaded = true;
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de charger le tableau de bord. Vérifiez votre connexion.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task OpenProductAsync(LowStockItem item) =>
        navigation.GoToAsync("product", new Dictionary<string, object> { ["Product"] = item.Product });

    [RelayCommand]
    private Task SeeAllLowStockAsync() =>
        navigation.GoToAsync("//products", new Dictionary<string, object> { ["LowStockOnly"] = true });

    [RelayCommand]
    private Task OpenProductsAsync() => navigation.GoToAsync("//products");

    [RelayCommand]
    private Task OpenWarehousesAsync() => navigation.GoToAsync("//warehouses");
}
