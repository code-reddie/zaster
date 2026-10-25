import { Pipe, PipeTransform } from '@angular/core';

const currency = new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' });
const signedCurrency = new Intl.NumberFormat('de-DE', {
  style: 'currency',
  currency: 'EUR',
  signDisplay: 'exceptZero',
});

/** 1234.5 → „1.234,50 €“, mit `signed` → „+1.234,50 €“. */
@Pipe({ name: 'amount' })
export class AmountPipe implements PipeTransform {
  transform(value: number | null | undefined, signed = false): string {
    return value == null ? '' : (signed ? signedCurrency : currency).format(value);
  }
}

/** DE89370400440532013000 → „DE89 3704 0044 0532 0130 00“ */
@Pipe({ name: 'iban' })
export class IbanPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return (value ?? '').replace(/(.{4})/g, '$1 ').trim();
  }
}

/** Datum als `yyyy-MM-dd` in Ortszeit (so erwartet es das Backend). */
export function isoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function parseIsoDate(value: string): Date {
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, m - 1, d);
}

const dayFormat = new Intl.DateTimeFormat('de-DE', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
});
const dayYearFormat = new Intl.DateTimeFormat('de-DE', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

/** „Heute“, „Gestern“ oder „Freitag, 9. Oktober“ (mit Jahr, wenn nicht dieses Jahr). */
export function dayLabel(value: string, today = new Date()): string {
  const date = parseIsoDate(value);
  const todayIso = isoDate(today);
  const yesterday = new Date(today);
  yesterday.setDate(today.getDate() - 1);
  if (value === todayIso) {
    return 'Heute';
  }
  if (value === isoDate(yesterday)) {
    return 'Gestern';
  }
  return (date.getFullYear() === today.getFullYear() ? dayFormat : dayYearFormat).format(date);
}

const monthFormat = new Intl.DateTimeFormat('de-DE', { month: 'long', year: 'numeric' });

export function monthLabel(year: number, month: number): string {
  return monthFormat.format(new Date(year, month, 1));
}

const shortMonthFormat = new Intl.DateTimeFormat('de-DE', { month: 'short', year: 'numeric' });

/** Kurzform für schmale Bildschirme, z. B. „Sept. 2026“. */
export function shortMonthLabel(year: number, month: number): string {
  return shortMonthFormat.format(new Date(year, month, 1));
}
