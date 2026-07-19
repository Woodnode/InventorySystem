using System.Globalization;
using InventoryMobile.Infrastructure.Api;

namespace InventoryMobile.Converters;

/// <summary>Inverse un booléen — utilisé pour désactiver un champ pendant <c>IsBusy</c>.</summary>
public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}

/// <summary>Vrai si la chaîne liée n'est ni nulle ni vide — pilote la visibilité des messages d'erreur.</summary>
public sealed class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Vrai si l'entier lié est strictement positif — pilote la visibilité du bandeau
/// "mouvements en attente" sur ProductsPage.</summary>
public sealed class IntGreaterThanZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Libellé français pour un <see cref="MobileMovementType"/> affiché dans un Picker.</summary>
public sealed class MovementTypeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            MobileMovementType.In => "Entrée",
            MobileMovementType.Out => "Sortie",
            MobileMovementType.Transfer => "Transfert",
            _ => value?.ToString() ?? string.Empty,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
