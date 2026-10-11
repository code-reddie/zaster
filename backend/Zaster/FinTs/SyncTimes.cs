using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zaster.FinTs;

/// <summary>
/// Uhrzeiten für den automatischen Abruf eines Kontos, gespeichert als „HH:mm“.
/// </summary>
internal static class SyncTimes
{
    public const int Max = 24;

    public static TimeOnly? Parse(string value) =>
        TimeOnly.TryParseExact(value.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;

    /// <summary>
    /// Prüft und sortiert die Uhrzeiten, doppelte fallen weg. Null, wenn eine keine gültige Uhrzeit ist.
    /// </summary>
    public static List<string>? Normalize(IEnumerable<string> values)
    {
        var times = new SortedSet<TimeOnly>();
        foreach (var value in values)
        {
            if (Parse(value ?? "") is not { } time)
            {
                return null;
            }

            times.Add(time);
        }

        return times.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture)).ToList();
    }
}
