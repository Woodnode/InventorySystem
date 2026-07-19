using InventoryMobile.ViewModels;

namespace InventoryMobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Session déjà persistée (SecureStorage) : pas besoin de redemander les identifiants.
        if (await _viewModel.TryRestoreSessionAsync())
        {
            await Shell.Current.GoToAsync("//products");
        }
    }
}
