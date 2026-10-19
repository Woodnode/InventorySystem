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
        await _viewModel.InitializeAsync();
    }

    private void OnEmailCompleted(object? sender, EventArgs e) => PasswordEntry.Focus();

    // Entrée sur le mot de passe : on ferme le clavier avant de partir, sinon il restait ouvert
    // par-dessus le tableau de bord.
    private void OnPasswordCompleted(object? sender, EventArgs e)
    {
        PasswordEntry.Unfocus();
        if (_viewModel.LoginCommand.CanExecute(null))
        {
            _viewModel.LoginCommand.Execute(null);
        }
    }
}
