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
/// Enregistrement d'un mouvement de stock pour un produit donné. Le produit arrive via la
/// navigation Shell (<see cref="ApplyQueryAttributes"/>), pas via un rechargement réseau :
/// <see cref="ProductsViewModel"/> l'a déjà en mémoire. Sans connexion (ou en cas de panne
/// réseau), le mouvement est mis en file d'attente locale via <see cref="ILocalMovementQueue"/>
/// plutôt que perdu — voir <see cref="QueueLocallyAsync"/>.
/// </summary>
public sealed partial class MovementViewModel : ObservableObject, IQueryAttributable
{
    private readonly IInventoryApi _api;
    private readonly ILocalMovementQueue _localQueue;
    private readonly IConnectivityChecker _connectivity;

    public MovementViewModel(IInventoryApi api, ILocalMovementQueue localQueue, IConnectivityChecker connectivity)
    {
        _api = api;
        _localQueue = localQueue;
        _connectivity = connectivity;
    }

    public ObservableCollection<WarehouseResponse> Warehouses { get; } = [];

    public IReadOnlyList<MobileMovementType> AvailableTypes { get; } =
        [MobileMovementType.In, MobileMovementType.Out, MobileMovementType.Transfer];

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

    /// <summary>Contrôle l'affichage du sélecteur d'entrepôt de destination dans la vue.</summary>
    public bool IsTransfer => SelectedType == MobileMovementType.Transfer;

    partial void OnSelectedTypeChanged(MobileMovementType value)
    {
        OnPropertyChanged(nameof(IsTransfer));
        if (value != MobileMovementType.Transfer)
        {
            SelectedToWarehouse = null;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Product", out var value) && value is ProductResponse product)
        {
            Product = product;
        }
    }

    public async Task InitializeAsync()
    {
        if (Warehouses.Count > 0) return; // déjà chargés (retour depuis un mouvement précédent)

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var warehouses = await _api.GetWarehousesAsync();
            Warehouses.Clear();
            foreach (var warehouse in warehouses.Where(w => w.IsActive).OrderBy(w => w.Name))
            {
                Warehouses.Add(warehouse);
            }
            SelectedWarehouse = Warehouses.FirstOrDefault();
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

            SuccessMessage = "Mouvement enregistré.";
            ResetForm();
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

        SuccessMessage = "Pas de connexion : mouvement mis en file d'attente, sera synchronisé automatiquement.";
        ResetForm();
    }

    private void ResetForm()
    {
        Quantity = 1;
        Reason = null;
    }
}
