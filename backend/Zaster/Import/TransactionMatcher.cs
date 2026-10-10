using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Zaster.Import;

/// <summary>
/// Erkennt Buchungen, die schon im Konto liegen, egal ob sie per CSV oder FinTS kamen.
/// </summary>
internal static class TransactionMatcher
{
    // FinTS (MT940) kürzt Namen auf Felder zu je 27 Zeichen, die CSV enthält den vollen Namen.
    // Ein kurzer Anfang reicht, weil Datum und Betrag ohnehin gleich sein müssen.
    private const int NameLength = 16;

    /// <summary>
    /// Vergleicht Buchungsdatum, Betrag und Auftraggeber. Verwendungszweck und Valuta bleiben außen vor,
    /// weil CSV und FinTS sie unterschiedlich formatieren.
    /// </summary>
    public static string Key(DateTimeOffset buchung, decimal betrag, string auftragsgeber) =>
        string.Join(
            '|',
            buchung.UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal.Round(betrag, 2).ToString("0.00", CultureInfo.InvariantCulture),
            NormalizeName(auftragsgeber));

    /// <summary>
    /// Gibt die Buchungen zurück, die noch nicht im Konto liegen. Gleiche Buchungen am selben Tag
    /// (etwa zwei gleiche Kaffeekäufe) werden gezählt, damit keine echte Buchung verloren geht.
    /// </summary>
    public static List<T> SelectNew<T>(
        IEnumerable<string> existingKeys,
        IEnumerable<T> incoming,
        Func<T, string> keySelector)
    {
        var remaining = existingKeys
            .GroupBy(k => k)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<T>();
        foreach (var item in incoming)
        {
            var key = keySelector(item);
            if (remaining.TryGetValue(key, out var count) && count > 0)
            {
                remaining[key] = count - 1;
            }
            else
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Großbuchstaben und Ziffern ohne Akzente. Umlaute werden einheitlich behandelt, egal ob
    /// die Bank „Müller“, „Mueller“ oder „Muller“ schickt.
    /// </summary>
    private static string NormalizeName(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.ToUpperInvariant().Replace("ß", "SS").Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        builder.Replace("AE", "A").Replace("OE", "O").Replace("UE", "U");
        return builder.Length > NameLength ? builder.ToString(0, NameLength) : builder.ToString();
    }
}
