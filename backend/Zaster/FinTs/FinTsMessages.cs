using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Zaster.FinTs;

/// <summary>
/// Maskieren für FinTS-Nachrichten und Auslesen der Bankrückmeldungen, wo libfintx 1.4.0 Lücken hat.
/// </summary>
internal static partial class FinTsMessages
{
    /// <summary>
    /// Maskiert die FinTS-Sonderzeichen. libfintx 1.4.0 tut das für die PIN nicht;
    /// unmaskierte Sonderzeichen machen die Nachricht unlesbar (ING-Meldung 9030).
    /// </summary>
    public static string Escape(string value) => value
        .Replace("?", "??")
        .Replace("+", "?+")
        .Replace(":", "?:")
        .Replace("'", "?'")
        .Replace("@", "?@");

    /// <summary>
    /// Die Kennung maskiert libfintx teilweise selbst, nur ? und @ fehlen.
    /// </summary>
    public static string EscapeUserId(string value) => value.Replace("?", "??").Replace("@", "?@");

    /// <summary>
    /// Nur Codes 9xxx sind Ablehnungen. 3xxx sind Warnungen (z. B. 3010: nur die letzten 90 Tage),
    /// die Umsätze kommen trotzdem.
    /// </summary>
    public static bool IsRejected(IEnumerable<FinTsBankMessage> messages) =>
        messages.Any(m => m.Code.StartsWith('9'));

    /// <summary>
    /// Liest die Rückmeldungen aus HIRMG und HIRMS selbst. libfintx übersieht sie, wenn der
    /// Segmentkopf einen zusätzlichen Doppelpunkt hat (z. B. <c>HIRMG:2:2:+9800::…</c>).
    /// </summary>
    public static List<FinTsBankMessage> ParseResultMessages(string? rawData) =>
        ResultSegments(rawData)
            .SelectMany(segment => SplitUnescaped(segment, '+').Skip(1))
            .Select(element => ResultMessageRegex().Match(element))
            .Where(match => match.Success)
            .Select(match => new FinTsBankMessage(match.Groups[1].Value, Unescape(match.Groups[2].Value).Trim()))
            .ToList();

    /// <summary>
    /// Die Rückmeldungssegmente HIRMG und HIRMS enthalten nur Codes und Texte der Bank, keine Kontodaten.
    /// </summary>
    public static IEnumerable<string> ResultSegments(string? rawData) =>
        string.IsNullOrEmpty(rawData)
            ? []
            : SplitUnescaped(rawData, '\'')
                .Where(segment => segment.StartsWith("HIRMG:", StringComparison.Ordinal) || segment.StartsWith("HIRMS:", StringComparison.Ordinal))
                .Select(segment => segment.Length > 500 ? segment[..500] + "…" : segment);

    private static List<string> SplitUnescaped(string value, char separator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '?' && i + 1 < value.Length)
            {
                current.Append(value[i]).Append(value[++i]);
            }
            else if (value[i] == separator)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(value[i]);
            }
        }

        parts.Add(current.ToString());
        return parts;
    }

    private static string Unescape(string value) => EscapeRegex().Replace(value, "$1");

    [GeneratedRegex(@"^\s*(\d{4}):[^:]*:(.*)$", RegexOptions.Singleline)]
    private static partial Regex ResultMessageRegex();

    [GeneratedRegex(@"\?(.)", RegexOptions.Singleline)]
    private static partial Regex EscapeRegex();
}
