using System;
using System.Collections.Generic;

namespace Zaster.Reports;

/// <summary>
/// Geldflüsse eines Zeitraums. <paramref name="Categories"/> enthält je Kategorie den verrechneten Betrag
/// (nur die eigenen Buchungen, ohne Unterkategorien; positiv = Einnahme, negativ = Ausgabe).
/// Buchungen ohne Kategorie stehen getrennt nach Einnahmen und Ausgaben in
/// <paramref name="OhneKategorieEinnahmen"/> und <paramref name="OhneKategorieAusgaben"/> (negativ).
/// </summary>
public sealed record FlowsDto(
    IReadOnlyList<CategoryFlowDto> Categories,
    decimal OhneKategorieEinnahmen,
    decimal OhneKategorieAusgaben,
    int Buchungen);

public sealed record CategoryFlowDto(int CategoryId, decimal Betrag);

public sealed record FlowsQuery(DateOnly? From, DateOnly? To);
