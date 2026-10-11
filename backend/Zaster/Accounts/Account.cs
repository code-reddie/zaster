using System;
using System.Collections.Generic;
using Zaster.Categories;
using Zaster.Database;
using Zaster.Transactions;
using Zaster.Users;

namespace Zaster.Accounts;

public sealed record Account : Entity
{
    public required string Name { get; set; }

    public required string Iban { get; init; }

    /// <summary>
    /// FinTS-Benutzerkennung, falls sie nicht aus der IBAN abgeleitet werden soll.
    /// </summary>
    public string? FinTsUserId { get; set; }

    /// <summary>
    /// Mit ASP.NET Data Protection verschlüsselte PIN. Verlässt das Backend nie.
    /// </summary>
    public string? FinTsPin { get; set; }

    /// <summary>
    /// Uhrzeiten („HH:mm“, Zeitzone <c>FinTS:TimeZone</c>), zu denen Zaster das Konto automatisch abruft.
    /// Läuft nur mit gespeicherter PIN. Leer = kein automatischer Abruf.
    /// </summary>
    public List<string> SyncTimes { get; set; } = [];

    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// Fehlermeldung des letzten fehlgeschlagenen Abrufs, null nach einem erfolgreichen.
    /// </summary>
    public string? LastSyncError { get; set; }

    /// <summary>
    /// Mitglieder des Kontos. Alle sind gleichberechtigt.
    /// </summary>
    public List<User> Users { get; init; } = [];

    public List<Transaction> Transactions { get; init; } = [];

    public List<Category> Categories { get; init; } = [];

    public List<Rule> Rules { get; init; } = [];
}
