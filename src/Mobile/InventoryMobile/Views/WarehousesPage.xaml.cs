using InventoryMobile.ViewModels;

namespace InventoryMobile.Views;

public partial class WarehousesPage : ContentPage
{
    private readonly WarehousesViewModel _viewModel;

    public WarehousesPage(WarehousesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
