using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Application.Connectivity;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Persistence;
using InventoryMobile.Services;
using Refit;

namespace InventoryMobile.ViewModels;

/// <summary>
/// Formulaire d'enregistrement d'un mouvement de stock, posé dans la fiche produit
/// (<see cref="ProductDetailViewModel"/>) comme sur le web. Le type et les entrepôts se
/// choisissent par segments et puces plutôt que par listes déroulantes : toutes les options
/// restent visibles. Sans connexion (ou en cas de panne réseau), le mouvement est mis en file
/// d'attente locale via <see cref="ILocalMovementQueue"/> plutôt que perdu.
/// </summary>
public sealed partial class MovementViewModel : ObservableObject
{
    private readonly IInventoryApi _api;
    private readonly ILocalMovementQueue _localQueue;
    private readonly IConnectivityChecker _connectivity;

    public MovementViewModel(IInventoryApi api, ILocalMovementQueue localQueue, IConnectivityChecker connectivity)
    {
        _api = api;
        _localQueue = localQueue;
        _connectivity = connectivity;
        TypeChoices = OptionChoice.Single(
            AvailableTypes.Select(t => (t, FrenchFormat.MovementType(t))), SelectedType, type => SelectedType = type);
    }

    /// <summary>Déclenché après un enregistrement en ligne réussi : la fiche recharge stock et historique.</summary>
    public event EventHandler? Recorded;

    public ObservableCollection<WarehouseResponse> Warehouses { get; } = [];

    public IReadOnlyList<MobileMovementType> AvailableTypes { get; } =
        [MobileMovementType.In, MobileMovementType.Out, MobileMovementType.Transfer];

    public IReadOnlyList<OptionChoice> TypeChoices { get; }

    public ObservableCollection<OptionChoice> WarehouseChoices { get; } = [];

    public ObservableCollection<OptionChoice> ToWarehouseChoices { get; } = [];

    [ObservableProperty]
    private ProductResponse? _product;

    [ObservableProperty]
    private MobileMovementType _selectedType = MobileMovementType.In;

    [ObservableProperty]
    private WarehouseResponse? _selectedWarehouse;

    [ObservableProperty]
    private WarehouseResponse? _selectedToWarehouse;

    [ObservableProperty]
    private int _quantity = 1;

    [ObservableProperty]
    private string? _reason;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    /// <summary>Contrôle l'affichage du choix de l'entrepôt de destination.</summary>
    public bool IsTransfer => SelectedType == MobileMovementType.Transfer;

    public string SourceLabel => IsTransfer ? "Depuis l'entrepôt" : "Entrepôt";

    partial void OnSelectedTypeChanged(MobileMovementType value)
    {
        OptionChoice.SelectValue(TypeChoices, value);
        ErrorMessage = null;
        OnPropertyChanged(nameof(IsTransfer));
        OnPropertyChanged(nameof(SourceLabel));
        if (value != MobileMovementType.Transfer)
        {
            SelectedToWarehouse = null;
        }
    }

    // Un nouveau choix efface l'erreur de la tentative précédente : elle ne décrit plus le formulaire.
    partial void OnSelectedWarehouseChanged(WarehouseResponse? value)
    {
        OptionChoice.SelectValue(WarehouseChoices, value);
        ErrorMessage = null;
    }

    partial void OnSelectedToWarehouseChanged(WarehouseResponse? value)
    {
        OptionChoice.SelectValue(ToWarehouseChoices, value);
        ErrorMessage = null;
    }

    /// <summary>Entrepôts déjà chargés par la fiche : seuls les actifs reçoivent un mouvement.</summary>
    public void UseWarehouses(IEnumerable<WarehouseResponse> warehouses)
    {
        Warehouses.Clear();
        WarehouseChoices.Clear();
        ToWarehouseChoices.Clear();
        foreach (var warehouse in warehouses.Where(w => w.IsActive).OrderBy(w => w.Name))
        {
            Warehouses.Add(warehouse);
            WarehouseChoices.Add(new OptionChoice(warehouse, warehouse.Name, c => SelectedWarehouse = (WarehouseResponse)c.Value));
            ToWarehouseChoices.Add(new OptionChoice(warehouse, warehouse.Name, c => SelectedToWarehouse = (WarehouseResponse)c.Value));
        }

        SelectedWarehouse = Warehouses.FirstOrDefault(w => w.Id == SelectedWarehouse?.Id) ?? Warehouses.FirstOrDefault();
        OptionChoice.SelectValue(WarehouseChoices, SelectedWarehouse);
    }

    public async Task InitializeAsync()
    {
        if (Warehouses.Count > 0) return; // déjà chargés (retour depuis un mouvement précédent)

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            UseWarehouses(await _api.GetWarehousesAsync());
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de charger les entrepôts.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Increment() => Quantity++;

    [RelayCommand]
    private void Decrement()
    {
        if (Quantity > 1) Quantity--;
    }

    [RelayCommand]
    private async Task RecordAsync()
    {
        if (IsBusy || Product is null) return;

        if (SelectedWarehouse is null)
        {
            ErrorMessage = "Sélectionnez un entrepôt.";
            return;
        }

        if (Quantity <= 0)
        {
            ErrorMessage = "La quantité doit être supérieure à zéro.";
            return;
        }

        if (SelectedType == MobileMovementType.Transfer)
        {
            if (SelectedToWarehouse is null)
            {
                ErrorMessage = "Sélectionnez l'entrepôt de destination.";
                return;
            }

            if (SelectedToWarehouse.Id == SelectedWarehouse.Id)
            {
                ErrorMessage = "L'entrepôt de destination doit être différent de l'entrepôt source.";
                return;
            }
        }

        var clientGuid = Guid.NewGuid();
        Guid? toWarehouseId = SelectedType == MobileMovementType.Transfer ? SelectedToWarehouse!.Id : null;
        var reason = string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim();

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            SuccessMessage = null;

            if (!_connectivity.IsConnected)
            {
                await QueueLocallyAsync(clientGuid, toWarehouseId, reason);
                return;
            }

            var request = new RecordMovementRequest(
                Product.Id, SelectedWarehouse.Id, SelectedType, Quantity, reason, toWarehouseId, clientGuid);

            await _api.RecordMovementAsync(request);

            // « Transfert » est masculin, « Entrée » et « Sortie » féminins.
            SuccessMessage = $"{FrenchFormat.MovementType(SelectedType)} de {Quantity} " +
                (SelectedType == MobileMovementType.Transfer ? "enregistré." : "enregistrée.");
            ResetForm();
            Recorded?.Invoke(this, EventArgs.Empty);
        }
        catch (ApiException ex) when (
            ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            // Serveur atteint : rejet métier (400) ou conflit (409). Remettre en file
            // offline échouerait de la même façon — afficher l'erreur, ne pas enqueuer.
            ErrorMessage = await ApiErrorReader.ReadDetailAsync(ex) ?? "Requête invalide.";
        }
        catch (ApiException ex) when (
            ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // Session expirée / droits insuffisants : ne jamais mettre en file offline
            // (le sync échouerait en boucle). AuthHeaderHandler gère déjà la redirection login.
            ErrorMessage = "Session expirée. Veuillez vous reconnecter.";
        }
        catch (Exception)
        {
            // La connectivité semblait disponible (IsConnected valait true) mais l'appel a
            // quand même échoué (panne de transport, DNS, timeout) : file d'attente locale.
            await QueueLocallyAsync(clientGuid, toWarehouseId, reason);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task QueueLocallyAsync(Guid clientGuid, Guid? toWarehouseId, string? reason)
    {
        await _localQueue.EnqueueAsync(new LocalMovement
        {
            ClientGuid = clientGuid,
            ProductId = Product!.Id,
            WarehouseId = SelectedWarehouse!.Id,
            ToWarehouseId = toWarehouseId,
            Type = (int)SelectedType,
            Quantity = Quantity,
            Reason = reason,
        });

        SuccessMessage = "Pas de connexion : le mouvement est gardé sur l'appareil et partira dès le retour du réseau.";
        ResetForm();
    }

    private void ResetForm()
    {
        Quantity = 1;
        Reason = null;
    }
}
