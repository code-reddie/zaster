using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Zaster.Categories;
using Zaster.Database;
using Zaster.Notifications;

namespace Zaster.Transactions;

/// <summary>
/// Legt Buchungen in einem Konto an, überspringt solche, die dort schon liegen,
/// und wendet die Regeln des Kontos auf die neuen an. Gemeinsamer Weg für CSV und FinTS.
/// Bleiben neue Buchungen ohne Kategorie, bekommen die Mitglieder des Kontos eine Benachrichtigung.
/// </summary>
public sealed class TransactionImporter(AppDbContext context, TimeProvider timeProvider, OpenTransactionsQueue notifications)
{
    public async Task<ImportResult> ImportAsync(
        int accountId,
        IReadOnlyList<ImportTransaction> incoming,
        TransactionSource source,
        CancellationToken cancellationToken)
    {
        // Ohne Kategorien laden: Nur der Kontostand wird bei vorhandenen Buchungen nachgetragen.
        var existing = await context.Transactions
            .Where(t => t.AccountId == accountId)
            .ToListAsync(cancellationToken);

        var matches = TransactionMatcher.MatchImport(existing, incoming);

        // Buchungen aus älteren Abrufen kennen ihren Kontostand noch nicht; ein neuer Abruf oder CSV-Import ergänzt ihn.
        foreach (var (item, match) in matches)
        {
            if (match is { Saldo: null } && item.Saldo is { } saldo)
            {
                match.Saldo = saldo;
            }
        }

        var newItems = matches.Where(m => m.Existing is null).Select(m => m.Item).ToList();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transactions = newItems
            .Select(t => new Transaction
            {
                AccountId = accountId,
                Buchung = t.Buchung,
                Valuta = t.Valuta ?? t.Buchung,
                Auftraggeber = t.Auftraggeber.Trim(),
                Buchungstext = t.Buchungstext.Trim(),
                Verwendungszweck = t.Verwendungszweck?.Trim() ?? string.Empty,
                Betrag = t.Betrag,
                Saldo = t.Saldo,
                ExterneId = Normalize(t.ExterneId),
                Zeitpunkt = t.Zeitpunkt?.ToUniversalTime(),
                Gebuehr = t.Gebuehr,
                Steuer = t.Steuer,
                Isin = Normalize(t.Isin),
                Stueck = t.Stueck,
                Kurs = t.Kurs,
                Fremdbetrag = t.Fremdbetrag,
                Fremdwaehrung = Normalize(t.Fremdwaehrung),
                Devisenkurs = t.Devisenkurs,
                GegenIban = Normalize(t.GegenIban),
                Art = Normalize(t.Art),
                Anlageklasse = Normalize(t.Anlageklasse),
                Mcc = Normalize(t.Mcc),
                // Ohne Geldbewegung (z. B. ein Aktiensplit) gibt es nichts einzuordnen.
                Reviewed = t.Betrag == 0,
                Source = source,
                CreatedAt = now,
            })
            .ToList();

        var rules = await context.Rules.Include(r => r.Conditions).Where(r => r.AccountId == accountId).ToListAsync(cancellationToken);
        RuleEngine.Apply(transactions, rules);

        context.Transactions.AddRange(transactions);
        await context.SaveChangesAsync(cancellationToken);

        var open = transactions.Count(t => !t.IsCategorized && !t.Reviewed);
        if (open > 0)
        {
            notifications.Enqueue(accountId, open);
        }

        return new ImportResult(transactions.Count, incoming.Count - transactions.Count);
    }

    /// <summary>
    /// Prüft die Längen der Zusatzfelder, damit keine beliebig großen Texte in der Datenbank landen.
    /// </summary>
    /// <returns>Deutsche Fehlermeldung oder null.</returns>
    public static string? Validate(IReadOnlyList<ImportTransaction> incoming)
    {
        foreach (var t in incoming)
        {
            if (t.ExterneId?.Length > 100 || t.Isin?.Length > 20 || t.Fremdwaehrung?.Length > 10 || t.GegenIban?.Length > 40
                || t.Art?.Length > 50 || t.Anlageklasse?.Length > 50 || t.Mcc?.Length > 10)
            {
                return $"Die Buchung vom {t.Buchung:dd.MM.yyyy} über {t.Betrag} € enthält ein zu langes Feld.";
            }
        }

        var ids = incoming.Select(t => Normalize(t.ExterneId)).OfType<string>().ToList();
        return ids.Count == ids.Distinct().Count()
            ? null
            : "Die Datei enthält dieselbe Transaktions-ID mehrmals.";
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
