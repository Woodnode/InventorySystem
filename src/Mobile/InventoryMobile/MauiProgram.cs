using System.Globalization;
using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Connectivity;
using InventoryMobile.Application.Navigation;
using InventoryMobile.Application.Notifications;
using InventoryMobile.Infrastructure;
using InventoryMobile.Services;
using InventoryMobile.ViewModels;
using InventoryMobile.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
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
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSans");
				fonts.AddFont("OpenSans-SemiBold.ttf", "OpenSansSemiBold");
				fonts.AddFont("OpenSans-Bold.ttf", "OpenSansBold");
				fonts.AddFont("Poppins-SemiBold.ttf", "Poppins");
				fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
				fonts.AddFont("Phosphor-Regular.ttf", "PhosphorRegular");
				fonts.AddFont("Phosphor-Bold.ttf", "PhosphorBold");
				fonts.AddFont("Phosphor-Fill.ttf", "PhosphorFill");
			})
			.ConfigureMauiHandlers(handlers =>
			{
#if ANDROID
				handlers.AddHandler<Shell, TabBarShellRenderer>();
#endif
			});

		// L'application n'existe qu'en français : dates et nombres suivent le français canadien,
		// comme le web (toLocaleString('fr-CA')), quelle que soit la langue de l'appareil.
		CultureInfo.DefaultThreadCurrentCulture = FrenchFormat.Culture;
		CultureInfo.DefaultThreadCurrentUICulture = FrenchFormat.Culture;
		CultureInfo.CurrentCulture = FrenchFormat.Culture;
		CultureInfo.CurrentUICulture = FrenchFormat.Culture;

		RemoveNativeFieldChrome();

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

		builder.Services.AddSingleton<AccountActions>();

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<ProductsViewModel>();
		builder.Services.AddTransient<ProductsPage>();
		builder.Services.AddTransient<MovementViewModel>();
		builder.Services.AddTransient<ProductDetailViewModel>();
		builder.Services.AddTransient<ProductDetailPage>();
		builder.Services.AddTransient<MovementsViewModel>();
		builder.Services.AddTransient<MovementsPage>();
		builder.Services.AddTransient<WarehousesViewModel>();
		builder.Services.AddTransient<WarehousesPage>();
		builder.Services.AddTransient<ScanViewModel>();
		builder.Services.AddTransient<ScanPage>();

		builder.Services.AddSingleton<AppShell>();

		return builder.Build();
	}

	// Les champs portent leur bordure arrondie (style Field) : on retire le soulignement natif
	// d'Android et la bordure de Windows, qui se superposaient au cadre.
	private static void RemoveNativeFieldChrome()
	{
#if ANDROID
		static void ClearUnderline(Android.Views.View view) =>
			view.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);

		EntryHandler.Mapper.AppendToMapping("SansSoulignement", (handler, _) => ClearUnderline(handler.PlatformView));
		PickerHandler.Mapper.AppendToMapping("SansSoulignement", (handler, _) => ClearUnderline(handler.PlatformView));
#elif WINDOWS
		EntryHandler.Mapper.AppendToMapping("SansBordure", (handler, _) =>
			handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0));
#endif
	}
}
