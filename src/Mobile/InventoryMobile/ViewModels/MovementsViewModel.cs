using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Sync;
using InventoryMobile.Services;

namespace InventoryMobile.ViewModels;

/// <summary>Ligne du journal : produit, sens et lieu du mouvement, quantité signée, date.</summary>
public sealed class RecentMovementItem(RecentMovementResponse movement)
{
    public Guid ProductId { get; } = movement.ProductId;

    public string ProductName { get; } = movement.ProductName;

    public string ProductSku { get; } = movement.ProductSku;

    public string Detail { get; } = string.Join(", ", new[]
    {
        FrenchFormat.MovementType(movement.Type),
        movement.ToWarehouseName is { } to ? $"{movement.WarehouseName} vers {to}" : movement.WarehouseName,
        movement.Reason,
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string Date { get; } = FrenchFormat.DateTime(movement.CreatedAtUtc);

    public MovementSign Sign { get; } = new(movement.Type, movement.Quantity);
}

/// <summary>
/// Journal des derniers mouvements, tous produits confondus (écran Mouvements du web). Montre
/// aussi les mouvements saisis hors ligne qui attendent le réseau, avec un envoi manuel.
/// </summary>
public sealed partial class MovementsViewModel(
    IInventoryApi api, IMovementSyncService syncService, INavigationService navigation) : ObservableObject
{
    private const int RecentCount = 30;

    public ObservableCollection<RecentMovementItem> Movements { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _syncMessage;

    [ObservableProperty]
    private int _pendingSyncCount;

    public string PendingSyncLabel => PendingSyncCount > 1
        ? $"{PendingSyncCount} mouvements attendent le réseau pour être envoyés."
        : "1 mouvement attend le réseau pour être envoyé.";

    public bool IsEmpty => IsLoaded && Movements.Count == 0;

    partial void OnPendingSyncCountChanged(int value) => OnPropertyChanged(nameof(PendingSyncLabel));

    public async Task InitializeAsync()
    {
        await SyncAsync();
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

            var recent = await api.GetRecentMovementsAsync(RecentCount);
            Movements.Clear();
            foreach (var movement in recent)
            {
                Movements.Add(new RecentMovementItem(movement));
            }

            IsLoaded = true;
        }
        catch (Exception)
        {
            ErrorMessage = "Impossible de charger les mouvements. Vérifiez votre connexion.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task SendPendingAsync()
    {
        var before = PendingSyncCount;
        await SyncAsync();
        SyncMessage = PendingSyncCount == 0
            ? $"{FrenchFormat.Count(before, "mouvement envoyé", "mouvements envoyés")}."
            : "Le réseau est encore indisponible. Les mouvements restent sur l'appareil.";
        if (PendingSyncCount < before)
        {
            await LoadAsync();
        }
    }

    private async Task SyncAsync()
    {
        try
        {
            PendingSyncCount = (await syncService.SyncPendingAsync()).RemainingPendingCount;
        }
        catch (Exception)
        {
            // Best-effort : la file reste intacte, un prochain passage réessaiera.
        }
    }

    [RelayCommand]
    private Task OpenProductAsync(RecentMovementItem item) =>
        navigation.GoToAsync("product", new Dictionary<string, object> { ["ProductId"] = item.ProductId });
}
