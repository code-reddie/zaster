import { ImportTransaction } from '../transactions/transaction.models';

/**
 * Liest Kontoumsätze aus einer CSV mit Semikolon, deutschem Datum und Zahlenformat. Zwei Aufbauten
 * kommen vor, beide mit einer Kopfzeile „Buchung;…“, über die die Spalten gefunden werden:
 *
 * - der Umsatz-Export der ING: Kopfbereich mit IBAN usw., dann
 *   `Buchung;Wertstellungsdatum;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung`
 * - eine eigene Liste (z. B. aus Excel oder Numbers) ohne Kopfbereich:
 *   `Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Betrag;Saldo`,
 *   Beträge teils mit „€“, Verwendungszwecke teils in Anführungszeichen über mehrere Zeilen.
 */
export function parseCsv(content: string): ImportTransaction[] {
  const records = splitRecords(content.replace(/^﻿/, ''));
  const headerIndex = records.findIndex((r) => r[0]?.trim() === 'Buchung');
  if (headerIndex === -1) {
    return [];
  }

  const header = records[headerIndex].map((h) => h.trim());
  const column = (match: (name: string) => boolean, fallback: number) => {
    const index = header.findIndex(match);
    return index === -1 ? fallback : index;
  };
  const columns = {
    buchung: column((h) => h === 'Buchung', 0),
    valuta: column((h) => h.startsWith('Wertstellung') || h === 'Valuta', 1),
    auftraggeber: column((h) => h.startsWith('Auftraggeber'), 2),
    buchungstext: column((h) => h === 'Buchungstext', 3),
    verwendungszweck: column((h) => h === 'Verwendungszweck', 4),
    saldo: column((h) => h === 'Saldo', -1),
    betrag: column((h) => h === 'Betrag', 7),
  };

  return records
    .slice(headerIndex + 1)
    .filter((c) => isGermanDate(c[columns.buchung]))
    .map((c) => ({
      buchung: toIsoDate(c[columns.buchung]),
      valuta: isGermanDate(c[columns.valuta])
        ? toIsoDate(c[columns.valuta])
        : toIsoDate(c[columns.buchung]),
      auftraggeber: c[columns.auftraggeber]?.trim() ?? '',
      buchungstext: c[columns.buchungstext]?.trim() ?? '',
      verwendungszweck: singleLine(c[columns.verwendungszweck] ?? ''),
      betrag: parseGermanNumber(c[columns.betrag] ?? ''),
      saldo: optionalNumber(c[columns.saldo]),
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

/**
 * Zerlegt die Datei in Zeilen und Spalten. Semikolons und Zeilenumbrüche innerhalb von
 * Anführungszeichen gehören zum Feld, "" steht für ein Anführungszeichen. Leere Zeilen fallen weg.
 */
function splitRecords(content: string): string[][] {
  const records: string[][] = [];
  let record: string[] = [];
  let current = '';
  let quoted = false;
  const endRecord = () => {
    record.push(current);
    if (record.length > 1 || record[0].trim() !== '') {
      records.push(record);
    }
    record = [];
    current = '';
  };

  for (let i = 0; i < content.length; i++) {
    const char = content[i];
    if (char === '"') {
      if (quoted && content[i + 1] === '"') {
        current += '"';
        i++;
      } else {
        quoted = !quoted;
      }
    } else if (char === ';' && !quoted) {
      record.push(current);
      current = '';
    } else if ((char === '\n' || char === '\r') && !quoted) {
      if (char === '\r' && content[i + 1] === '\n') {
        i++;
      }
      endRecord();
    } else {
      current += char;
    }
  }
  if (current !== '' || record.length > 0) {
    endRecord();
  }
  return records;
}

/** Mehrzeilige Verwendungszwecke werden zu einer Zeile. */
function singleLine(value: string): string {
  return value
    .split(/\r?\n/)
    .map((part) => part.trim())
    .filter((part) => part.length > 0)
    .join(' ');
}

function isGermanDate(value: string | undefined): value is string {
  return !!value && /^\d{2}\.\d{2}\.\d{4}$/.test(value.trim());
}

/** 04.05.2026 → 2026-05-04 */
function toIsoDate(value: string): string {
  const [day, month, year] = value.trim().split('.');
  return `${year}-${month}-${day}`;
}

/** Ältere Exporte ohne Spalte „Saldo“: kein Kontostand. */
function optionalNumber(value: string | undefined): number | null {
  const number = parseGermanNumber(value ?? '');
  return Number.isFinite(number) ? number : null;
}

/** -1.000,00 → -1000, „1.448,42 €“ → 1448.42. Alles außer Ziffern, Vorzeichen und Komma fällt weg. */
function parseGermanNumber(value: string): number {
  const cleaned = value.replace(/[^\d,-]/g, '').replace(',', '.');
  return /^-?\d+(\.\d+)?$/.test(cleaned) ? Number.parseFloat(cleaned) : Number.NaN;
}
