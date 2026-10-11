using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaster.Transactions;

/// <summary>
/// Prüft über die Kontostände der Bank, ob Buchungen fehlen: Kontostand vor einer Buchung + Betrag muss
/// den Kontostand danach ergeben, und jeder Tag muss mit dem Endstand des Vortags beginnen.
/// </summary>
/// <remarks>
/// Die Reihenfolge innerhalb eines Tages ist nicht gespeichert (die CSV ist absteigend sortiert, FinTS
/// aufsteigend). Deshalb wird pro Tag die Kette gesucht: Der Anfangsstand ist der einzige Stand „vorher“,
/// der bei keiner anderen Buchung als Stand „nachher“ vorkommt, der Endstand umgekehrt.
/// Buchungen von Hand haben keinen Kontostand und bleiben außen vor. Buchungen von Bank oder CSV ohne
/// Kontostand (aus Abrufen vor dieser Prüfung) werden zwischen zwei bekannten Ständen mitgezählt.
/// </remarks>
internal static class BalanceCheck
{
    public sealed record Entry(DateOnly Buchung, decimal Betrag, decimal? Saldo, TransactionSource Source);

    public static BalanceCheckDto Check(IEnumerable<Entry> entries)
    {
        var gaps = new List<BalanceGap>();
        decimal? knownEnd = null;
        DateOnly? knownDay = null;
        decimal pending = 0;
        var checkedCount = 0;
        var withoutCount = 0;

        var days = entries
            .Where(e => e.Saldo is not null || e.Source != TransactionSource.Manuell)
            .GroupBy(e => e.Buchung)
            .OrderBy(g => g.Key);

        foreach (var day in days)
        {
            var withSaldo = day.Where(e => e.Saldo is not null).ToList();
            var without = day.Where(e => e.Saldo is null).ToList();
            checkedCount += withSaldo.Count;
            withoutCount += without.Count;

            if (withSaldo.Count == 0)
            {
                pending += without.Sum(e => e.Betrag);
                continue;
            }

            decimal? expected = knownEnd is { } previous ? previous + pending : null;
            if (!TryChain(withSaldo, expected, out var start, out var end))
            {
                gaps.Add(new BalanceGap(knownDay, day.Key, null));
                knownEnd = null;
                knownDay = null;
                pending = 0;
                continue;
            }

            if (expected is { } startExpected && start != startExpected)
            {
                gaps.Add(new BalanceGap(knownDay, day.Key, start - startExpected));
            }

            knownEnd = end;
            knownDay = day.Key;
            pending = 0;
        }

        gaps.Reverse();
        return new BalanceCheckDto(knownEnd, knownDay, checkedCount, withoutCount, gaps);
    }

    /// <summary>
    /// Sucht Anfangs- und Endstand eines Tages. Klappt nicht, wenn die Kette unterbrochen ist,
    /// also mitten am Tag eine Buchung fehlt.
    /// </summary>
    private static bool TryChain(List<Entry> entries, decimal? expected, out decimal start, out decimal end)
    {
        // +1 für jeden Stand vorher, −1 für jeden Stand nachher. In einer lückenlosen Kette bleibt
        // genau der Anfangsstand mit +1 und der Endstand mit −1 übrig.
        var balance = new Dictionary<decimal, int>();
        foreach (var entry in entries)
        {
            var after = entry.Saldo!.Value;
            var before = after - entry.Betrag;
            balance[before] = balance.GetValueOrDefault(before) + 1;
            balance[after] = balance.GetValueOrDefault(after) - 1;
        }

        var starts = balance.Where(b => b.Value != 0).ToList();
        if (starts.Count == 0)
        {
            // Der Tag endet mit dem Stand, mit dem er begann (z. B. Buchung und Rückbuchung).
            // Welcher Stand das ist, entscheidet der Vortag; sonst der erste Stand vorher.
            var befores = entries.Select(e => e.Saldo!.Value - e.Betrag).ToList();
            start = end = expected is { } previous && befores.Contains(previous) ? previous : befores[0];
            return true;
        }

        if (starts.Count == 2 && starts.Any(b => b.Value == 1) && starts.Any(b => b.Value == -1))
        {
            start = starts.First(b => b.Value == 1).Key;
            end = starts.First(b => b.Value == -1).Key;
            return true;
        }

        start = end = 0;
        return false;
    }
}
