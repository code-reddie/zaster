import { ImportTransaction } from '../transactions/transaction.models';

/**
 * Liest den Umsatz-Export der ING (CSV mit Semikolon, deutsches Datum und Zahlenformat).
 * Die Spalten werden über die Kopfzeile gefunden, weil die ING sie gelegentlich ergänzt.
 */
export function parseIngCsv(content: string): ImportTransaction[] {
  const lines = content.replace(/^﻿/, '').split(/\r?\n/);
  const headerIndex = lines.findIndex((l) => l.startsWith('Buchung;'));
  if (headerIndex === -1) {
    return [];
  }

  const header = splitLine(lines[headerIndex]);
  const column = (match: (name: string) => boolean, fallback: number) => {
    const index = header.findIndex((h) => match(h.trim()));
    return index === -1 ? fallback : index;
  };
  const columns = {
    buchung: column((h) => h === 'Buchung', 0),
    valuta: column((h) => h.startsWith('Wertstellung') || h === 'Valuta', 1),
    auftraggeber: column((h) => h.startsWith('Auftraggeber'), 2),
    buchungstext: column((h) => h === 'Buchungstext', 3),
    verwendungszweck: column((h) => h === 'Verwendungszweck', 4),
    betrag: column((h) => h === 'Betrag', 7),
  };

  return lines
    .slice(headerIndex + 1)
    .filter((line) => line.trim().length > 0)
    .map(splitLine)
    .filter((c) => isGermanDate(c[columns.buchung]))
    .map((c) => ({
      buchung: toIsoDate(c[columns.buchung]),
      valuta: isGermanDate(c[columns.valuta])
        ? toIsoDate(c[columns.valuta])
        : toIsoDate(c[columns.buchung]),
      auftraggeber: c[columns.auftraggeber]?.trim() ?? '',
      buchungstext: c[columns.buchungstext]?.trim() ?? '',
      verwendungszweck: c[columns.verwendungszweck]?.trim() ?? '',
      betrag: parseGermanNumber(c[columns.betrag] ?? ''),
    }))
    .filter((t) => Number.isFinite(t.betrag));
}

/**
 * Die ING exportiert je nach Version UTF-8 oder Windows-1252. Ungültiges UTF-8 heißt Windows-1252.
 */
export function decodeCsv(bytes: ArrayBuffer): string {
  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  } catch {
    return new TextDecoder('windows-1252').decode(bytes);
  }
}

/** Trennt an Semikolons, außer innerhalb von Anführungszeichen. */
function splitLine(line: string): string[] {
  const result: string[] = [];
  let current = '';
  let quoted = false;
  for (let i = 0; i < line.length; i++) {
    const char = line[i];
    if (char === '"') {
      if (quoted && line[i + 1] === '"') {
        current += '"';
        i++;
      } else {
        quoted = !quoted;
      }
    } else if (char === ';' && !quoted) {
      result.push(current);
      current = '';
    } else {
      current += char;
    }
  }
  result.push(current);
  return result;
}

function isGermanDate(value: string | undefined): value is string {
  return !!value && /^\d{2}\.\d{2}\.\d{4}$/.test(value.trim());
}

/** 04.05.2026 → 2026-05-04 */
function toIsoDate(value: string): string {
  const [day, month, year] = value.trim().split('.');
  return `${year}-${month}-${day}`;
}

/** -1.000,00 → -1000 */
function parseGermanNumber(value: string): number {
  return Number.parseFloat(value.trim().replace(/\./g, '').replace(',', '.'));
}
