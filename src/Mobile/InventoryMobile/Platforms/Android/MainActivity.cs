using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.View;

namespace InventoryMobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // L'application n'existe qu'en français : les fenêtres natives (menu du compte, demande
    // d'accès à la caméra) suivent le français canadien, même sur un appareil réglé en anglais.
    protected override void AttachBaseContext(Context? @base)
    {
        if (@base?.Resources?.Configuration is { } current)
        {
            var locale = Java.Util.Locale.ForLanguageTag("fr-CA");
            Java.Util.Locale.Default = locale;
            var configuration = new Configuration(current);
            configuration.SetLocale(locale);
            @base = @base.CreateConfigurationContext(configuration);
        }

        base.AttachBaseContext(@base);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplySystemBarAppearance();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplySystemBarAppearance();
    }

    // Barre d'état aux couleurs du fond de page : icônes foncées en thème clair, claires en sombre.
    private void ApplySystemBarAppearance()
    {
        if (Window is null || WindowCompat.GetInsetsController(Window, Window.DecorView) is not { } controller)
        {
            return;
        }

        var isDark = (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
        controller.AppearanceLightStatusBars = !isDark;
        controller.AppearanceLightNavigationBars = !isDark;
    }
}
