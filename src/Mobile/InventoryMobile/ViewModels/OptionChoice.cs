using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InventoryMobile.ViewModels;

/// <summary>
/// Option d'un sélecteur à puces ou à segments (type de mouvement, entrepôt, filtre de
/// stock). Remplace les Picker natifs : toutes les options restent visibles, un seul toucher
/// suffit pour choisir.
/// </summary>
public sealed partial class OptionChoice(object value, string label, Action<OptionChoice> onSelected) : ObservableObject
{
    public object Value { get; } = value;

    public string Label { get; } = label;

    [ObservableProperty]
    private bool _isSelected;

    [RelayCommand]
    private void Select() => onSelected(this);

    /// <summary>Groupe à choix unique : choisir une option désélectionne les autres.</summary>
    public static List<OptionChoice> Single<T>(IEnumerable<(T Value, string Label)> options, T? selected, Action<T> apply)
        where T : notnull
    {
        var group = new List<OptionChoice>();
        foreach (var (value, label) in options)
        {
            group.Add(new OptionChoice(value, label, chosen =>
            {
                SelectValue(group, chosen.Value);
                apply((T)chosen.Value);
            })
            {
                IsSelected = selected is not null && EqualityComparer<T>.Default.Equals(value, selected),
            });
        }

        return group;
    }

    public static void SelectValue(IEnumerable<OptionChoice> group, object? value)
    {
        foreach (var choice in group)
        {
            choice.IsSelected = Equals(choice.Value, value);
        }
    }
}
