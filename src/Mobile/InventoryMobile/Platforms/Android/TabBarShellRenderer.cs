using Android.Content.Res;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Shape;
using InventoryMobile.Services;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;

namespace InventoryMobile;

/// <summary>
/// Shell Android dont l'onglet actif porte une pastille teintée, comme le lien actif de la
/// navigation du web. Le thème Material 2 de MAUI ne donne aucune forme à cet indicateur :
/// sans celle-ci, il restait invisible.
/// </summary>
public sealed class TabBarShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem) =>
        new TabBarTracker(this, shellItem);
}

internal sealed class TabBarTracker(IShellContext shellContext, ShellItem shellItem)
    : ShellBottomNavViewAppearanceTracker(shellContext, shellItem)
{
    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);
        ApplyIndicator(bottomView);
    }

    public override void ResetAppearance(BottomNavigationView bottomView)
    {
        base.ResetAppearance(bottomView);
        ApplyIndicator(bottomView);
    }

    private static void ApplyIndicator(BottomNavigationView bottomView)
    {
        var density = bottomView.Resources?.DisplayMetrics?.Density ?? 1;
        bottomView.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityLabeled;
        bottomView.ItemActiveIndicatorEnabled = true;
        bottomView.ItemActiveIndicatorColor = ColorStateList.ValueOf(ThemeColors.Get("AccentSoft").ToPlatform());
        bottomView.ItemActiveIndicatorWidth = (int)(64 * density);
        bottomView.ItemActiveIndicatorHeight = (int)(32 * density);
        bottomView.ItemActiveIndicatorShapeAppearance = ShapeAppearanceModel.InvokeBuilder()
            .SetAllCorners(CornerFamily.Rounded, 10 * density)
            .Build();
    }
}
