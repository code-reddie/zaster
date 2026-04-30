using System;
using System.Data.Common;

namespace Zaster.Models;

public sealed record Transaction : Entity
{
    public required DateTimeOffset Buchung { get; init; }

    public required DateTimeOffset Valuta { get; init; }

    public required string Auftragsgeber { get; init; }

    public required string Buchungstext { get; init; }

    public string Verwendungszweck { get; init; } = string.Empty;

    public required decimal Betrag { get; init; }

    public int AccountId { get; init; }

    public Account? Account { get; init; }

    public int? CategoryId { get; init; }

    public Category? Category { get; init; }
}

public sealed record TransactionDto(
    int Id,
    DateTimeOffset Buchung,
    DateTimeOffset Valuta,
    string Auftragsgeber,
    string Buchungstext,
    string Verwendungszweck,
    decimal Betrag,
    int AccountId,
    int? CategoryId);

public sealed record CreateTransaction(
    DateTimeOffset Buchung,
    DateTimeOffset Valuta,
    string Auftragsgeber,
    string Buchungstext,
    string? Verwendungszweck,
    decimal Betrag,
    int AccountId);