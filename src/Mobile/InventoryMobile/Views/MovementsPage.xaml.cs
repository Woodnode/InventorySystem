using InventoryMobile.ViewModels;

namespace InventoryMobile.Views;

public partial class MovementsPage : ContentPage
{
    private readonly MovementsViewModel _viewModel;

    public MovementsPage(MovementsViewModel viewModel)
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
