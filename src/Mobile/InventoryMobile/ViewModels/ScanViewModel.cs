using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Services;
using Refit;

namespace InventoryMobile.ViewModels;

/// <summary>
/// ViewModel du scan QR / code-barres (voir plan §8.1). Le code scanné == le SKU du
/// produit (pas de champ "barcode" séparé côté backend). MVVM Community Toolkit :
/// [ObservableProperty] et [RelayCommand] — zéro code-behind métier.
/// </summary>
public sealed partial class ScanViewModel : ObservableObject
{
    private readonly IInventoryApi _api;

    public ScanViewModel(IInventoryApi api)
    {
        _api = api;
    }

    /// <summary>Le scan caméra n'est pas supporté par ZXing.Net.Maui sur Windows — voir ScanPage.xaml.</summary>
    public bool IsWindowsUnsupported { get; } = OperatingSystem.IsWindows();

    [ObservableProperty]
    private string? _lastScannedCode;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Appelé quand un code est détecté : recherche par SKU côté API, puis navigation vers
    /// l'écran de mouvement (même mécanisme que ProductsViewModel.SelectProductAsync).
    /// </summary>
    [RelayCommand]
    private async Task OnBarcodeDetectedAsync(string code)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(code)) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            LastScannedCode = code;

            var product = await _api.GetProductBySkuAsync(code);

            await Shell.Current.GoToAsync(
                "movement",
                new Dictionary<string, object> { ["Product"] = product });
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            ErrorMessage = $"Aucun produit trouvé pour le code « {code} ».";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            ErrorMessage = await ApiErrorReader.ReadDetailAsync(ex) ?? "Code invalide.";
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de rechercher ce produit. Vérifiez votre connexion.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
