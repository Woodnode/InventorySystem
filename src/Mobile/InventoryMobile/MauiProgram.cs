using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Connectivity;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Application.Notifications;
using InventoryMobile.Infrastructure;
using InventoryMobile.Services;
using InventoryMobile.ViewModels;
using InventoryMobile.Views;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using SQLitePCL;
using ZXing.Net.Maui.Controls;

namespace InventoryMobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// Doit précéder toute utilisation de sqlite-net-pcl (ILocalMovementQueue) : sans cet
		// appel, la première requête SQLite échoue avec "unable to load e_sqlite3" (bundle
		// SQLitePCLRaw.bundle_e_sqlite3 — voir InventoryMobile.Infrastructure.csproj).
		Batteries_V2.Init();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseBarcodeReader()
			.UseLocalNotification()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// ITokenStore/IConnectivityChecker dépendent de SecureStorage/Connectivity (Microsoft.
		// Maui.Essentials) : seul ce head project y a accès, Infrastructure ne connaît que
		// l'abstraction.
		builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
		builder.Services.AddSingleton<IConnectivityChecker, MauiConnectivityChecker>();
		builder.Services.AddSingleton<ILocalNotificationService, PluginLocalNotificationService>();
		builder.Services.AddSingleton<INavigationService, MauiNavigationService>();
		builder.Services.AddSingleton<ISessionExpiredNotifier, MauiSessionExpiredNotifier>();
		// Écoute app-wide (login -> logout), pas seulement pendant que ProductsPage est
		// affichée — voir AUDIT.md M-4.
		builder.Services.AddSingleton<IStockAlertSessionListener, StockAlertSessionListener>();

		var localDatabasePath = Path.Combine(FileSystem.AppDataDirectory, "inventory_offline.db3");
		builder.Services.AddInventoryInfrastructure(ApiConfig.BaseUrl, localDatabasePath, ApiConfig.StockHubUrl);

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<ProductsViewModel>();
		builder.Services.AddTransient<ProductsPage>();
		builder.Services.AddTransient<MovementViewModel>();
		builder.Services.AddTransient<MovementPage>();
		builder.Services.AddTransient<ScanViewModel>();
		builder.Services.AddTransient<ScanPage>();

		builder.Services.AddSingleton<AppShell>();

		return builder.Build();
	}
}
