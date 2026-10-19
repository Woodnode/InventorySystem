using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Services;
using Refit;

namespace InventoryMobile.ViewModels;

/// <summary>
/// ViewModel du scan QR / code-barres (voir plan §8.1). Le code scanné == le SKU du
/// produit (pas de champ "barcode" séparé côté backend).
/// </summary>
public sealed partial class ScanViewModel : ObservableObject
{
    private readonly IInventoryApi _api;
    private readonly INavigationService _navigation;

    public ScanViewModel(IInventoryApi api, INavigationService navigation)
    {
        _api = api;
        _navigation = navigation;
    }

    /// <summary>Le scan caméra n'est pas supporté par ZXing.Net.Maui sur Windows — voir ScanPage.xaml.</summary>
    public bool IsWindowsUnsupported { get; } = OperatingSystem.IsWindows();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCamera))]
    [NotifyPropertyChangedFor(nameof(ShowPermissionDenied))]
    [NotifyPropertyChangedFor(nameof(IsDetecting))]
    private string? _lastScannedCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetecting))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCamera))]
    [NotifyPropertyChangedFor(nameof(ShowPermissionDenied))]
    [NotifyPropertyChangedFor(nameof(IsDetecting))]
    private bool _hasCameraPermission;

    public bool ShowCamera => !IsWindowsUnsupported && HasCameraPermission;

    /// <summary>Faux tant que la demande n'a pas abouti : le message de refus ne clignote pas à l'ouverture.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPermissionDenied))]
    private bool _permissionChecked;

    public bool ShowPermissionDenied => PermissionChecked && !IsWindowsUnsupported && !HasCameraPermission;

    public bool IsDetecting => ShowCamera && !IsBusy;

    /// <summary>Demande la permission caméra runtime (Android 6+ / iOS).</summary>
    public async Task EnsureCameraPermissionAsync()
    {
        if (IsWindowsUnsupported)
        {
            HasCameraPermission = false;
            return;
        }

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Camera>();

            HasCameraPermission = status == PermissionStatus.Granted;
            PermissionChecked = true;
            if (!HasCameraPermission)
                ErrorMessage = null;
        }
        catch (Exception)
        {
            // Appelée depuis OnAppearing (async void, voir ScanPage.xaml.cs) : une exception
            // plateforme sur la demande de permission ne doit jamais faire planter l'app —
            // au pire l'écran affiche "permission refusée" (voir ré-audit, incohérence avec
            // les autres ViewModels qui se protègent déjà de la même façon).
            HasCameraPermission = false;
            PermissionChecked = true;
            ErrorMessage = "Impossible de vérifier la permission caméra.";
        }
    }

    [RelayCommand]
    private static void OpenSettings() => AppInfo.Current.ShowSettingsUI();

    /// <summary>Code saisi à la main : étiquette abîmée, caméra refusée ou indisponible.</summary>
    [ObservableProperty]
    private string _manualCode = string.Empty;

    [RelayCommand]
    private async Task SearchManualCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualCode))
        {
            ErrorMessage = "Saisissez un SKU, par exemple LIV-0102.";
            return;
        }

        await LookUpAsync(ManualCode.Trim());
    }

    [RelayCommand]
    private async Task OnBarcodeDetectedAsync(string code)
    {
        if (!HasCameraPermission) return;

        await LookUpAsync(code);
    }

    private async Task LookUpAsync(string code)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(code)) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            LastScannedCode = code;

            var product = await _api.GetProductBySkuAsync(code);

            await _navigation.GoToAsync(
                "product",
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
