using System.Globalization;

namespace InventorySystem.Application.Common;

/// <summary>
/// Lecture des nombres venus d'un classeur, ou le separateur decimal depend de la
/// langue de saisie.
/// </summary>
public static class NumberParsing
{
    /// <summary>
    /// Lit un nombre decimal ecrit indifferemment a la francaise ou a l'anglaise.
    ///
    /// L'import passait <c>NumberStyles.Any</c> avec la culture invariante : ce style
    /// autorise le separateur de milliers, et la virgule en est un pour cette culture.
    /// « 0,6 » etait donc lu comme 6 et « 0,41 » comme 41 — les poids saisis dans un
    /// classeur francophone se sont retrouves multiplies par dix ou par cent.
    /// </summary>
    public static decimal? ParseDecimal(string? valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            return null;

        // Les espaces, y compris insecables, servent de separateur de milliers en francais.
        var v = valeur.Trim()
            .Replace(" ", string.Empty)
            .Replace("\u00a0", string.Empty)
            .Replace("\u202f", string.Empty);

        var virgule = v.LastIndexOf(',');
        var point = v.LastIndexOf('.');

        // Le separateur decimal est le dernier des deux ; l'autre ne peut etre
        // qu'un separateur de milliers.
        if (virgule >= 0 && point >= 0)
        {
            v = virgule > point
                ? v.Replace(".", string.Empty).Replace(',', '.')
                : v.Replace(",", string.Empty);
        }
        else if (virgule >= 0)
        {
            // Une seule virgule : decimale francaise. Plusieurs : milliers anglais.
            v = v.IndexOf(',') == virgule ? v.Replace(',', '.') : v.Replace(",", string.Empty);
        }

        // Float autorise le signe, les decimales et l'exposant, mais pas les milliers :
        // plus aucun separateur ambigu ne subsiste a ce stade.
        return decimal.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }
}
