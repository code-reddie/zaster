using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Zaster.Transactions;

/// <summary>
/// Erkennt, ob ein Suchbegriff ein Geldbetrag ist, z. B. „12,50“, „-12,50 €“, „1.234,56“ oder „12.50“.
/// Komma ist das Dezimalzeichen; ein Punkt gilt als Tausenderpunkt, wenn genau drei Ziffern folgen
/// („1.234“ = 1234), sonst als Dezimalpunkt („12.50“). Stehen beide da, ist das letzte das Dezimalzeichen.
/// </summary>
internal static partial class AmountSearch
{
    /// <summary>
    /// Liefert den Betrag (höchstens zwei Nachkommastellen) und ob ein Vorzeichen angegeben war.
    /// Ohne Vorzeichen findet die Suche Einnahmen und Ausgaben mit diesem Betrag.
    /// </summary>
    public static bool TryParse(string text, out decimal amount, out bool signed)
    {
        amount = 0;
        signed = false;

        var match = AmountPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var sign = match.Groups["sign"].Value;
        var number = match.Groups["number"].Value;
        signed = sign.Length > 0;

        var lastComma = number.LastIndexOf(',');
        var lastDot = number.LastIndexOf('.');
        string normalized;
        if (lastComma >= 0 && lastDot >= 0)
        {
            var decimalSeparator = lastComma > lastDot ? ',' : '.';
            var groupSeparator = decimalSeparator == ',' ? "." : ",";
            normalized = number.Replace(groupSeparator, string.Empty).Replace(decimalSeparator, '.');
        }
        else if (lastComma >= 0)
        {
            normalized = number.Replace(',', '.');
        }
        else if (lastDot >= 0 && ThousandsOnly().IsMatch(number))
        {
            normalized = number.Replace(".", string.Empty);
        }
        else
        {
            normalized = number;
        }

        // Mehr als ein Dezimalzeichen oder mehr als zwei Nachkommastellen: kein Betrag.
        var dot = normalized.IndexOf('.');
        if (dot != normalized.LastIndexOf('.') || (dot >= 0 && normalized.Length - dot - 1 is 0 or > 2))
        {
            return false;
        }

        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        amount = signed && sign != "+" ? -value : value;
        return true;
    }

    [GeneratedRegex(@"^(?<sign>[+\-−]?)\s*(?<number>\d[\d.,]*)\s*(€|EUR)?$", RegexOptions.IgnoreCase)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+$")]
    private static partial Regex ThousandsOnly();
}
