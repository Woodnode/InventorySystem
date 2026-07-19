using InventoryMobile.ViewModels;

namespace InventoryMobile.Views;

public partial class MovementPage : ContentPage
{
    private readonly MovementViewModel _viewModel;

    public MovementPage(MovementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
