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
    /// Ordnet Buchungen eines Imports den vorhandenen zu. Bringt eine Buchung eine <c>ExterneId</c> mit, die
    /// es im Konto schon gibt, ist sie dieselbe. Sonst entscheidet der Schlüssel aus Datum, Betrag und
    /// Auftraggeber, aber nur gegen vorhandene Buchungen ohne eigene Kennung: Zwei Buchungen mit
    /// verschiedenen Kennungen sind nie dieselbe, auch wenn sie gleich aussehen.
    /// </summary>
    public static List<(ImportTransaction Item, Transaction? Existing)> MatchImport(
        IReadOnlyList<Transaction> existing,
        IReadOnlyList<ImportTransaction> incoming)
    {
        var byId = existing
            .Where(t => t.ExterneId is not null)
            .GroupBy(t => t.ExterneId!)
            .ToDictionary(g => g.Key, g => g.First());

        var withoutId = existing.Where(t => t.ExterneId is null);
        var keyed = incoming.Where(t => !IsKnownId(t)).ToList();
        var keyMatches = Match(
                withoutId,
                t => Key(t.Buchung, t.Betrag, t.Auftraggeber),
                keyed,
                t => Key(t.Buchung, t.Betrag, t.Auftraggeber))
            .ToDictionary(m => m.Item, m => m.Existing, ReferenceEqualityComparer.Instance);

        return incoming
            .Select(t => (t, IsKnownId(t) ? byId[t.ExterneId!.Trim()] : (Transaction?)keyMatches[t]))
            .ToList();

        bool IsKnownId(ImportTransaction t) => t.ExterneId is { } id && byId.ContainsKey(id.Trim());
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
