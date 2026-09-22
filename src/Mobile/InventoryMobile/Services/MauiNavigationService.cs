using InventoryMobile.Application.Navigation;

namespace InventoryMobile.Services;

/// <summary>Adapter MAUI Shell pour <see cref="INavigationService"/>.</summary>
public sealed class MauiNavigationService : INavigationService
{
    public Task GoToAsync(string route)
        => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(route));

    public Task GoToAsync(string route, IDictionary<string, object> parameters)
        => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(route, parameters));
}
