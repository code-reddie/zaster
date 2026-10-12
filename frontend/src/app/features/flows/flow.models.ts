import { CategoryNode } from '../categories/category.models';

/** Antwort von `GET /api/accounts/{id}/flows`. */
export interface Flows {
  /** Verrechneter Betrag je Kategorie, nur eigene Buchungen (positiv = Einnahme). */
  categories: { categoryId: number; betrag: number }[];
  ohneKategorieEinnahmen: number;
  /** Negativ. */
  ohneKategorieAusgaben: number;
  buchungen: number;
}

export type FlowSide = 'in' | 'out';

export type FlowKind = 'category' | 'direct' | 'none' | 'balance';

/** Knoten im Diagramm: eine Kategorie (mit Unterkategorien) auf der Einnahmen- oder Ausgabenseite. */
export interface FlowNode {
  /** Eindeutig über beide Seiten, z. B. `out:12`; merkt sich, was aufgeklappt ist. */
  key: string;
  side: FlowSide;
  kind: FlowKind;
  label: string;
  /** Voller Pfad für Tooltips, z. B. „Auto › Passat“. */
  title: string;
  /** Betrag ohne Vorzeichen. */
  value: number;
  /** Farbe als `#rrggbb`, null = neutral. */
  color: string | null;
  children: FlowNode[];
}

export interface FlowTree {
  income: FlowNode[];
  expense: FlowNode[];
  /** Summe der Einnahmen (positiv). */
  einnahmen: number;
  /** Summe der Ausgaben (positiv). */
  ausgaben: number;
}

/**
 * Baut aus den Beträgen je Kategorie zwei Bäume, links die Einnahmen, rechts die Ausgaben.
 * Eine Kategorie steht auf der Seite, zu der ihr verrechneter Betrag passt; eine Elternkategorie
 * kann daher auf beiden Seiten vorkommen. Hat eine Kategorie eigene Buchungen und Unterkategorien,
 * bekommen die eigenen Buchungen einen Knoten „Ohne Unterkategorie“, damit die Teile die Summe ergeben.
 * Die Differenz gleicht ein Knoten „Überschuss“ (rechts) oder „Aus dem Kontostand“ (links) aus.
 */
export function buildFlowTree(flows: Flows, categories: CategoryNode[]): FlowTree {
  const own = new Map(flows.categories.map((c) => [c.categoryId, c.betrag]));
  const children = new Map<number | null, CategoryNode[]>();
  for (const category of categories) {
    const list = children.get(category.parentId) ?? [];
    list.push(category);
    children.set(category.parentId, list);
  }

  const build = (side: FlowSide, parentId: number | null): FlowNode[] => {
    const sign = side === 'in' ? 1 : -1;
    const nodes: FlowNode[] = [];
    for (const category of children.get(parentId) ?? []) {
      const sub = build(side, category.id);
      const direct = Math.max(0, sign * (own.get(category.id) ?? 0));
      const value = direct + sum(sub);
      if (value <= 0) {
        continue;
      }
      if (sub.length > 0 && direct > 0) {
        sub.push({
          key: `${side}:${category.id}:direkt`,
          side,
          kind: 'direct',
          label: 'Ohne Unterkategorie',
          title: `${category.path} (ohne Unterkategorie)`,
          value: direct,
          color: category.effectiveColor,
          children: [],
        });
      }
      nodes.push({
        key: `${side}:${category.id}`,
        side,
        kind: 'category',
        label: category.name,
        title: category.path,
        value,
        color: category.effectiveColor,
        children: sortByValue(sub),
      });
    }
    return sortByValue(nodes);
  };

  const income = build('in', null);
  const expense = build('out', null);
  if (flows.ohneKategorieEinnahmen > 0) {
    income.push(uncategorized('in', flows.ohneKategorieEinnahmen));
  }
  if (flows.ohneKategorieAusgaben < 0) {
    expense.push(uncategorized('out', -flows.ohneKategorieAusgaben));
  }

  const einnahmen = sum(income);
  const ausgaben = sum(expense);
  const diff = round(einnahmen - ausgaben);
  if (diff > 0) {
    expense.push(balance('out', 'Überschuss', diff));
  } else if (diff < 0) {
    income.push(balance('in', 'Aus dem Kontostand', -diff));
  }

  return { income, expense, einnahmen, ausgaben };
}

function uncategorized(side: FlowSide, value: number): FlowNode {
  return {
    key: `${side}:ohne`,
    side,
    kind: 'none',
    label: 'Ohne Kategorie',
    title: 'Ohne Kategorie',
    value,
    color: null,
    children: [],
  };
}

function balance(side: FlowSide, label: string, value: number): FlowNode {
  return {
    key: `${side}:saldo`,
    side,
    kind: 'balance',
    label,
    title: label,
    value,
    color: null,
    children: [],
  };
}

/** Größte zuerst, „Ohne Unterkategorie“ ans Ende. */
function sortByValue(nodes: FlowNode[]): FlowNode[] {
  return nodes.sort(
    (a, b) => Number(a.kind === 'direct') - Number(b.kind === 'direct') || b.value - a.value,
  );
}

function sum(nodes: FlowNode[]): number {
  return round(nodes.reduce((total, node) => total + node.value, 0));
}

/** Gegen Rundungsfehler beim Addieren von Cent-Beträgen. */
function round(value: number): number {
  return Math.round(value * 100) / 100;
}
