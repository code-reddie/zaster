export interface Category {
  id: number;
  name: string;
  color: string | null;
  parentId: number | null;
  sortOrder: number;
}

export interface SaveCategory {
  name: string;
  color: string | null;
  parentId: number | null;
}

/** Kategorie mit Lage im Baum, für Listen und Auswahl. */
export interface CategoryNode extends Category {
  depth: number;
  /** Namen von der obersten Ebene bis hier, z. B. „Autos › Passat“. */
  path: string;
  /** Eigene Farbe oder die der nächsten Elternkategorie. */
  effectiveColor: string | null;
  childCount: number;
}

export type RuleField = 'Alle' | 'Auftraggeber' | 'Buchungstext' | 'Verwendungszweck';

export const RULE_FIELDS: { value: RuleField; label: string }[] = [
  { value: 'Alle', label: 'Auftraggeber, Buchungstext oder Verwendungszweck' },
  { value: 'Auftraggeber', label: 'Auftraggeber' },
  { value: 'Buchungstext', label: 'Buchungstext' },
  { value: 'Verwendungszweck', label: 'Verwendungszweck' },
];

export type RuleDirection = 'Beide' | 'Eingang' | 'Ausgang';

export const RULE_DIRECTIONS: { value: RuleDirection; label: string }[] = [
  { value: 'Beide', label: 'Beide' },
  { value: 'Ausgang', label: 'Ausgaben' },
  { value: 'Eingang', label: 'Einnahmen' },
];

export interface Rule {
  id: number;
  pattern: string;
  field: RuleField;
  direction: RuleDirection;
  categoryId: number;
  sortOrder: number;
}

export interface SaveRule {
  pattern: string;
  field: RuleField;
  direction: RuleDirection;
  categoryId: number;
}

/** Farben, die im hellen und dunklen Modus gut lesbar sind. */
export const CATEGORY_COLORS = [
  '#ef4444',
  '#f97316',
  '#f59e0b',
  '#84cc16',
  '#22c55e',
  '#14b8a6',
  '#06b6d4',
  '#3b82f6',
  '#6366f1',
  '#a855f7',
  '#ec4899',
  '#78716c',
];

/**
 * Sortiert die flache Liste in Baumreihenfolge (Eltern vor Kindern, Geschwister nach sortOrder
 * und Name) und ergänzt Tiefe, Pfad und geerbte Farbe.
 */
export function buildTree(categories: Category[]): CategoryNode[] {
  const children = new Map<number | null, Category[]>();
  for (const category of categories) {
    const list = children.get(category.parentId) ?? [];
    list.push(category);
    children.set(category.parentId, list);
  }
  for (const list of children.values()) {
    list.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name, 'de'));
  }

  const result: CategoryNode[] = [];
  const visit = (parentId: number | null, depth: number, path: string, color: string | null) => {
    for (const category of children.get(parentId) ?? []) {
      const node: CategoryNode = {
        ...category,
        depth,
        path: path ? `${path} › ${category.name}` : category.name,
        effectiveColor: category.color ?? color,
        childCount: children.get(category.id)?.length ?? 0,
      };
      result.push(node);
      visit(category.id, depth + 1, node.path, node.effectiveColor);
    }
  };
  visit(null, 0, '', null);
  return result;
}

/** Die Kategorie und alle darunter. */
export function descendantIds(categories: Category[], id: number): Set<number> {
  const result = new Set([id]);
  let added = true;
  while (added) {
    added = false;
    for (const category of categories) {
      if (category.parentId !== null && result.has(category.parentId) && !result.has(category.id)) {
        result.add(category.id);
        added = true;
      }
    }
  }
  return result;
}
