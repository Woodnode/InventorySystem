using System.Globalization;
using InventoryMobile.Infrastructure.Api;

namespace InventoryMobile.Services;

/// <summary>
/// Mises en forme partagées avec le front web : nombres et dates en français canadien,
/// poids dans l'unité qui se lit le mieux, libellés des types de mouvement.
/// </summary>
public static class FrenchFormat
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-CA");

    public static string Number(int value) => value.ToString("N0", Culture);

    public static string Number(decimal value) => value.ToString("N0", Culture);

    /// <summary>Accord simple : « 1 référence », « 36 références ».</summary>
    public static string Count(int value, string singular, string plural) =>
        $"{Number(value)} {(value > 1 ? plural : singular)}";

    /// <summary>
    /// Poids en grammes (formatPoids du web) : un livre de poche pèse 220 g, une palette
    /// plusieurs centaines de kilos ; zéro n'est jamais une mesure et s'affiche en tiret.
    /// </summary>
    public static string Weight(decimal? grams)
    {
        if (grams is null || grams == 0)
        {
            return "-";
        }

        if (Math.Abs(grams.Value) < 1000)
        {
            return $"{Math.Round(grams.Value).ToString("N0", Culture)} g";
        }

        var kg = grams.Value / 1000;
        return $"{kg.ToString(Math.Abs(kg) < 100 ? "N1" : "N0", Culture)} kg";
    }

    public static string MovementType(MobileMovementType type) => type switch
    {
        MobileMovementType.In => "Entrée",
        MobileMovementType.Out => "Sortie",
        MobileMovementType.Transfer => "Transfert",
        _ => type.ToString(),
    };

    /// <summary>« 17 oct., 07 h 14 », en heure de l'appareil.</summary>
    public static string DateTime(DateTime utc)
    {
        var local = utc.Kind == DateTimeKind.Local ? utc : System.DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        return $"{local.ToString("d MMM", Culture)}, {local.ToString("HH' h 'mm", Culture)}";
    }

    public static string Date(DateTime? value) =>
        value is null ? "-" : value.Value.ToLocalTime().ToString("d MMMM yyyy", Culture);
}
