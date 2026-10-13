/** Vorlesetext und Tooltip für die Zahl der offenen Buchungen eines Kontos. */
export function openLabel(count: number): string {
  return count === 1 ? '1 Buchung ohne Kategorie' : `${count} Buchungen ohne Kategorie`;
}
