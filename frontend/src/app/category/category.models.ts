export interface Category {
  id: number;
  name: string;
  icon: string | null;
  color: string | null;
  description: string;
  parentCategoryId: number | null;
}

export interface CreateCategory {
  name: string;
  icon: string | null;
  color: string | null;
  description: string | null;
  parentCategoryId: number | null;
}

export type RuleField = 'Alle' | 'Auftragsgeber' | 'Buchungstext' | 'Verwendungszweck';

export const RULE_FIELDS: RuleField[] = [
  'Alle',
  'Auftragsgeber',
  'Buchungstext',
  'Verwendungszweck',
];

export interface CategorizationRule {
  id: number;
  pattern: string;
  field: RuleField;
  categoryId: number;
}

export interface CreateCategorizationRule {
  pattern: string;
  field: RuleField;
  categoryId: number;
}

export interface ApplyRulesResult {
  updated: number;
}
