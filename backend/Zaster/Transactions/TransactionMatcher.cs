using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Zaster.Transactions;

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
    public static string Key(DateOnly buchung, decimal betrag, string auftraggeber) =>
        string.Join(
            '|',
            buchung.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal.Round(betrag, 2).ToString("0.00", CultureInfo.InvariantCulture),
            NormalizeName(auftraggeber));

    /// <summary>
    /// Gibt die Buchungen zurück, die noch nicht im Konto liegen. Gleiche Buchungen am selben Tag
    /// (etwa zwei gleiche Kaffeekäufe) werden gezählt, damit keine echte Buchung verloren geht.
    /// </summary>
    public static List<T> SelectNew<T>(
        IEnumerable<string> existingKeys,
        IEnumerable<T> incoming,
        Func<T, string> keySelector) =>
        Match(existingKeys, k => k, incoming, keySelector)
            .Where(m => m.Existing is null)
            .Select(m => m.Item)
            .ToList();

    /// <summary>
    /// Ordnet jeder eingehenden Buchung die passende vorhandene zu, oder null, wenn sie neu ist.
    /// Jede vorhandene Buchung wird höchstens einmal zugeordnet.
    /// </summary>
    public static List<(T Item, TExisting? Existing)> Match<T, TExisting>(
        IEnumerable<TExisting> existing,
        Func<TExisting, string> existingKeySelector,
        IEnumerable<T> incoming,
        Func<T, string> keySelector)
        where TExisting : class
    {
        var remaining = existing
            .GroupBy(existingKeySelector)
            .ToDictionary(g => g.Key, g => new Queue<TExisting>(g));

        var result = new List<(T, TExisting?)>();
        foreach (var item in incoming)
        {
            result.Add(remaining.TryGetValue(keySelector(item), out var queue) && queue.TryDequeue(out var match)
                ? (item, match)
                : (item, null));
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
