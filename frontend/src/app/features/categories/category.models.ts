export interface Category {
  id: number;
  name: string;
  color: string | null;
  parentId: number | null;
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

export type RuleField =
  'Alle' | 'Auftraggeber' | 'Buchungstext' | 'Verwendungszweck' | 'Anlageklasse';

export const RULE_FIELDS: { value: RuleField; label: string }[] = [
  { value: 'Alle', label: 'Auftraggeber, Buchungstext oder Verwendungszweck' },
  { value: 'Auftraggeber', label: 'Auftraggeber' },
  { value: 'Buchungstext', label: 'Buchungstext' },
  { value: 'Verwendungszweck', label: 'Verwendungszweck' },
  // Nur bei Konten mit Anlageklassen (Trade Republic); der Wert muss genau passen.
  { value: 'Anlageklasse', label: 'Anlageklasse' },
];

export type RuleDirection = 'Beide' | 'Eingang' | 'Ausgang';

export const RULE_DIRECTIONS: { value: RuleDirection; label: string }[] = [
  { value: 'Beide', label: 'Beide' },
  { value: 'Ausgang', label: 'Ausgaben' },
  { value: 'Eingang', label: 'Einnahmen' },
];

/** Eine Bedingung: der Text kommt im Feld vor. Eine Regel passt, wenn alle Bedingungen passen. */
export interface RuleCondition {
  field: RuleField;
  pattern: string;
}

export interface Rule {
  id: number;
  conditions: RuleCondition[];
  direction: RuleDirection;
  categoryId: number;
  sortOrder: number;
}

export interface SaveRule {
  conditions: RuleCondition[];
  direction: RuleDirection;
  categoryId: number;
}

/**
 * Farben, die im hellen und dunklen Modus gut lesbar sind: 18 kräftige Farbtöne nach Farbkreis,
 * danach 6 dunkle Töne. Badges und Avatare mischen die Farbe mit der Textfarbe, damit auch die
 * dunklen Töne im dunklen Modus lesbar bleiben.
 */
export const CATEGORY_COLORS = [
  '#ef4444', // Rot
  '#f97316', // Orange
  '#f59e0b', // Bernstein
  '#eab308', // Gelb
  '#84cc16', // Limette
  '#22c55e', // Grün
  '#10b981', // Smaragd
  '#14b8a6', // Türkis
  '#06b6d4', // Cyan
  '#0ea5e9', // Himmelblau
  '#3b82f6', // Blau
  '#6366f1', // Indigo
  '#8b5cf6', // Veilchen
  '#a855f7', // Lila
  '#d946ef', // Fuchsia
  '#ec4899', // Pink
  '#f43f5e', // Rose
  '#78716c', // Steingrau
  '#b91c1c', // Dunkelrot
  '#b45309', // Braun
  '#15803d', // Dunkelgrün
  '#1d4ed8', // Dunkelblau
  '#7e22ce', // Dunkellila
  '#475569', // Schiefer
];

/**
 * Sortiert die flache Liste in Baumreihenfolge (Eltern vor Kindern, Geschwister alphabetisch) und
 * ergänzt Tiefe, Pfad und geerbte Farbe.
 */
export function buildTree(categories: Category[]): CategoryNode[] {
  const children = new Map<number | null, Category[]>();
  for (const category of categories) {
    const list = children.get(category.parentId) ?? [];
    list.push(category);
    children.set(category.parentId, list);
  }
  for (const list of children.values()) {
    list.sort((a, b) => compareText(a.name, b.name));
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

const collator = new Intl.Collator('de', { sensitivity: 'base', numeric: true });

/** Alphabetisch nach deutscher Sortierung, Groß-/Kleinschreibung egal, „Konto 2“ vor „Konto 10“. */
export function compareText(a: string, b: string): number {
  return collator.compare(a, b);
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
