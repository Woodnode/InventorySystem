using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Services;

namespace InventoryMobile.ViewModels;

/// <summary>
/// Entrepôts en lecture seule, avec recherche par nom ou adresse (écran Entrepôts du web).
/// Leur création et leur modification restent sur le web, réservées aux gestionnaires.
/// </summary>
public sealed partial class WarehousesViewModel(IInventoryApi api) : ObservableObject
{
    private List<WarehouseResponse> _all = [];

    public ObservableCollection<WarehouseResponse> Warehouses { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public string CountLabel
    {
        get
        {
            var inactive = _all.Count(w => !w.IsActive);
            var label = FrenchFormat.Count(_all.Count, "entrepôt", "entrepôts");
            return inactive > 0 ? $"{label}, {FrenchFormat.Count(inactive, "inactif", "inactifs")}" : label;
        }
    }

    public bool IsEmpty => IsLoaded && Warehouses.Count == 0;

    public string EmptyMessage => string.IsNullOrWhiteSpace(SearchText)
        ? "Aucun entrepôt pour l'instant."
        : $"Aucun entrepôt ne correspond à « {SearchText.Trim()} ».";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            _all = [.. (await api.GetWarehousesAsync()).OrderBy(w => !w.IsActive).ThenBy(w => w.Name)];
            IsLoaded = true;
            ApplyFilter();
            OnPropertyChanged(nameof(CountLabel));
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de charger les entrepôts. Vérifiez votre connexion.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var term = Normalize(SearchText);
        Warehouses.Clear();
        foreach (var warehouse in _all.Where(w => term.Length == 0 || Normalize($"{w.Name} {w.Address}").Contains(term)))
        {
            Warehouses.Add(warehouse);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    // Sans accents ni casse : « levis » trouve « Lévis ».
    private static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string([.. decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
    }
}
