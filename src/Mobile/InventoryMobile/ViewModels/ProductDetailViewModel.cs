using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Services;

namespace InventoryMobile.ViewModels;

/// <summary>Stock d'un entrepôt sur la fiche (bloc « Logistique » du web).</summary>
public sealed class StockItem(StockResponse stock, string warehouseName)
{
    public string WarehouseName { get; } = warehouseName;

    public string QuantityLabel { get; } = $"{FrenchFormat.Number(stock.Quantity)} unités";

    /// <summary>Emplacement physique : section, espace et palette, ce qu'on cherche dans l'allée.</summary>
    public string Location { get; } = string.Join("  ", new[]
    {
        stock.Section is { Length: > 0 } s ? $"Section {s}" : null,
        stock.Space is { Length: > 0 } e ? $"Espace {e}" : null,
        stock.Pallet is { Length: > 0 } p ? $"Palette {p}" : null,
    }.OfType<string>());

    public bool HasLocation => Location.Length > 0;

    public string Packaging { get; } = stock.BoxesCount > 0
        ? $"{FrenchFormat.Count(stock.BoxesCount, "boîte", "boîtes")} de {FrenchFormat.Number(stock.CopiesPerBox)} copies"
        : string.Empty;

    public bool HasPackaging => Packaging.Length > 0;

    public string? Distributor { get; } = stock.DistributorName;

    public bool HasDistributor => !string.IsNullOrWhiteSpace(stock.DistributorName);

    public string? Comment { get; } = stock.Comment;

    public bool HasComment => !string.IsNullOrWhiteSpace(stock.Comment);
}

/// <summary>Mouvement de l'historique d'un produit.</summary>
public sealed class HistoryItem(MovementResponse movement, Func<Guid, string> warehouseName)
{
    public string Title { get; } = FrenchFormat.MovementType(movement.Type);

    public string Detail { get; } = string.Join(", ", new[]
    {
        movement.ToWarehouseId is Guid to
            ? $"{warehouseName(movement.WarehouseId)} vers {warehouseName(to)}"
            : warehouseName(movement.WarehouseId),
        movement.Reason,
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string Date { get; } = FrenchFormat.DateTime(movement.CreatedAtUtc);

    public MovementSign Sign { get; } = new(movement.Type, movement.Quantity);
}

/// <summary>Quantité signée d'un mouvement : le signe dit le sens sans lire le libellé.</summary>
public sealed class MovementSign(MobileMovementType type, int quantity)
{
    public string Label { get; } = type switch
    {
        MobileMovementType.In => $"+{FrenchFormat.Number(quantity)}",
        MobileMovementType.Out => $"−{FrenchFormat.Number(quantity)}",
        _ => FrenchFormat.Number(quantity),
    };

    public bool IsIn { get; } = type == MobileMovementType.In;

    public bool IsOut { get; } = type == MobileMovementType.Out;

    public bool IsTransfer { get; } = type == MobileMovementType.Transfer;
}

/// <summary>Une caractéristique de la fiche (« Collection : Fournitures »).</summary>
public sealed record DetailField(string Label, string Value);

/// <summary>
/// Fiche produit du web : quantité et seuil, emplacements par entrepôt, enregistrement d'un
/// mouvement et historique. Le produit arrive par la navigation (liste, accueil ou scan) ou par
/// son id (journal des mouvements) ; il est toujours relu pour afficher le stock à jour.
/// </summary>
public sealed partial class ProductDetailViewModel : ObservableObject, IQueryAttributable
{
    private const int HistoryPageSize = 10;

    private readonly IInventoryApi _api;
    private List<WarehouseResponse> _warehouses = [];
    private Guid? _productId;
    private int _historyPage;

    public ProductDetailViewModel(IInventoryApi api, MovementViewModel form)
    {
        _api = api;
        Form = form;
        Form.Recorded += async (_, _) => await RefreshAfterMovementAsync();
    }

    public MovementViewModel Form { get; }

    public ObservableCollection<StockItem> Stocks { get; } = [];

    public ObservableCollection<HistoryItem> History { get; } = [];

    public ObservableCollection<DetailField> Fields { get; } = [];

    [ObservableProperty]
    private ProductResponse? _product;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _historyTotal;

    public string Subtitle => Product is null ? string.Empty
        : string.IsNullOrWhiteSpace(Product.Description) ? $"SKU {Product.Sku}" : $"SKU {Product.Sku}, {Product.Description}";

    public string StockLabel => Product is null ? string.Empty
        : $"{(Product.IsLowOnStock ? "Stock bas, " : "")}{FrenchFormat.Number(Product.Quantity)} en stock, min. {FrenchFormat.Number(Product.LowStockThreshold)}";

    public bool IsLowOnStock => Product?.IsLowOnStock == true;

    public bool HasStocks => Stocks.Count > 0;

    public bool HasHistory => History.Count > 0;

    public bool HasMoreHistory => History.Count < HistoryTotal;

    partial void OnProductChanged(ProductResponse? value)
    {
        Form.Product = value;
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(StockLabel));
        OnPropertyChanged(nameof(IsLowOnStock));

        Fields.Clear();
        if (value is null) return;

        void Add(string label, string? text) => Fields.Add(new DetailField(label, string.IsNullOrWhiteSpace(text) ? "-" : text));
        Add("Type", value.ProductType);
        Add("Collection", value.Collection);
        Add("Vol., N°", value.VolumeNumber);
        Add("Compagnie", value.Company);
        Add("Code projet", value.ProjectCode);
        Add("Année", value.Year?.ToString());
        Add("Poids unitaire", FrenchFormat.Weight(value.WeightPerCopyGrams));
        // Un produit sans stock donnerait un total de zéro, qui se lit comme une anomalie.
        Add("Poids total estimé", FrenchFormat.Weight(
            value.WeightPerCopyGrams is { } unit && value.Quantity > 0 ? unit * value.Quantity : null));
    }

    partial void OnHistoryTotalChanged(int value) => OnPropertyChanged(nameof(HasMoreHistory));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Product", out var value) && value is ProductResponse product)
        {
            _productId = product.Id;
            Product = product;
        }
        else if (query.TryGetValue("ProductId", out var id) && id is Guid productId)
        {
            _productId = productId;
        }
    }

    public async Task InitializeAsync()
    {
        if (IsLoaded) return; // retour depuis une page poussée par-dessus la fiche

        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy || _productId is not Guid id) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var productTask = _api.GetProductAsync(id);
            var warehousesTask = _api.GetWarehousesAsync();
            var stocksTask = _api.GetStocksByProductAsync(id);
            var historyTask = _api.GetMovementsByProductAsync(id, 1, HistoryPageSize);
            await Task.WhenAll(productTask, warehousesTask, stocksTask, historyTask);

            Product = productTask.Result;
            _warehouses = [.. warehousesTask.Result];
            Form.UseWarehouses(_warehouses);
            ShowStocks(stocksTask.Result);
            ShowHistory(historyTask.Result, append: false);
            IsLoaded = true;
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de charger la fiche. Vérifiez votre connexion.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreHistoryAsync()
    {
        if (_productId is not Guid id || !HasMoreHistory) return;

        try
        {
            ShowHistory(await _api.GetMovementsByProductAsync(id, _historyPage + 1, HistoryPageSize), append: true);
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de charger la suite de l'historique.";
        }
    }

    private async Task RefreshAfterMovementAsync()
    {
        if (_productId is not Guid id) return;

        try
        {
            var productTask = _api.GetProductAsync(id);
            var stocksTask = _api.GetStocksByProductAsync(id);
            var historyTask = _api.GetMovementsByProductAsync(id, 1, HistoryPageSize);
            await Task.WhenAll(productTask, stocksTask, historyTask);
            Product = productTask.Result;
            ShowStocks(stocksTask.Result);
            ShowHistory(historyTask.Result, append: false);
        }
        catch (Exception)
        {
            // Le mouvement est enregistré ; un rafraîchissement manuel suffira.
        }
    }

    private string WarehouseName(Guid id) => _warehouses.FirstOrDefault(w => w.Id == id)?.Name ?? "Entrepôt inconnu";

    private void ShowStocks(IReadOnlyList<StockResponse> stocks)
    {
        Stocks.Clear();
        foreach (var stock in stocks.OrderByDescending(s => s.Quantity))
        {
            Stocks.Add(new StockItem(stock, WarehouseName(stock.WarehouseId)));
        }

        OnPropertyChanged(nameof(HasStocks));
    }

    private void ShowHistory(PagedResponse<MovementResponse> page, bool append)
    {
        if (!append) History.Clear();
        foreach (var movement in page.Items)
        {
            History.Add(new HistoryItem(movement, WarehouseName));
        }

        _historyPage = page.Page;
        HistoryTotal = page.TotalCount;
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(HasMoreHistory));
    }
}
