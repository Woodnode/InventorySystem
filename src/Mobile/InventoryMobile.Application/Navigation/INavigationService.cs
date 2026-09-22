namespace InventoryMobile.Application.Navigation;

/// <summary>
/// Abstraction de navigation (DIP) : les ViewModels ne dépendent pas de
/// <c>Shell.Current</c> / Microsoft.Maui.Controls.
/// </summary>
public interface INavigationService
{
    Task GoToAsync(string route);

    Task GoToAsync(string route, IDictionary<string, object> parameters);
}
