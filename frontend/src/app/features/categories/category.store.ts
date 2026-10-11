import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { buildTree, Category, CategoryNode, Rule, SaveCategory, SaveRule } from './category.models';

/**
 * Kategorien und Regeln des gewählten Kontos. Wird von Buchungen, „Ohne Kategorie“ und der
 * Kategorien-Seite gemeinsam genutzt.
 */
@Injectable({ providedIn: 'root' })
export class CategoryStore {
  private readonly http = inject(HttpClient);

  private readonly accountId = signal<number | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly rules = signal<Rule[]>([]);
  readonly loading = signal(false);

  readonly tree = computed(() => buildTree(this.categories()));
  readonly byId = computed(() => new Map<number, CategoryNode>(this.tree().map((c) => [c.id, c])));

  /** Lädt Kategorien und Regeln, wenn das Konto gewechselt hat. */
  async use(accountId: number) {
    if (this.accountId() === accountId) {
      return;
    }
    this.accountId.set(accountId);
    this.categories.set([]);
    this.rules.set([]);
    await this.reload();
  }

  async reload() {
    const accountId = this.accountId();
    if (accountId === null) {
      return;
    }
    this.loading.set(true);
    try {
      const [categories, rules] = await Promise.all([
        firstValueFrom(this.http.get<Category[]>(this.url('categories'))),
        firstValueFrom(this.http.get<Rule[]>(this.url('rules'))),
      ]);
      if (this.accountId() === accountId) {
        this.categories.set(categories);
        this.rules.set(rules);
      }
    } finally {
      this.loading.set(false);
    }
  }

  async createCategory(dto: SaveCategory): Promise<Category> {
    const category = await firstValueFrom(this.http.post<Category>(this.url('categories'), dto));
    this.categories.update((c) => [...c, category]);
    return category;
  }

  async updateCategory(id: number, dto: SaveCategory) {
    const category = await firstValueFrom(
      this.http.put<Category>(this.url(`categories/${id}`), dto),
    );
    this.categories.update((c) => c.map((x) => (x.id === id ? category : x)));
  }

  /** Verschiebt eine Kategorie unter ihren Geschwistern um eine Stelle nach oben oder unten. */
  async moveCategory(id: number, direction: -1 | 1) {
    const category = this.categories().find((c) => c.id === id);
    if (!category) {
      return;
    }
    const siblings = this.tree().filter((c) => c.parentId === category.parentId);
    const index = siblings.findIndex((c) => c.id === id);
    const target = index + direction;
    if (target < 0 || target >= siblings.length) {
      return;
    }
    const ids = siblings.map((c) => c.id);
    [ids[index], ids[target]] = [ids[target], ids[index]];
    this.categories.update((all) =>
      all.map((c) => (ids.includes(c.id) ? { ...c, sortOrder: ids.indexOf(c.id) } : c)),
    );
    await firstValueFrom(
      this.http.put(this.url('categories/order'), { parentId: category.parentId, ids }),
    );
  }

  /** Löscht die Kategorie samt Unterkategorien; deren Regeln löscht das Backend mit. */
  async deleteCategory(id: number) {
    await firstValueFrom(this.http.delete(this.url(`categories/${id}`)));
    await this.reload();
  }

  async createRule(dto: SaveRule): Promise<Rule> {
    const rule = await firstValueFrom(this.http.post<Rule>(this.url('rules'), dto));
    this.rules.update((r) => [...r, rule]);
    return rule;
  }

  async updateRule(id: number, dto: SaveRule) {
    const rule = await firstValueFrom(this.http.put<Rule>(this.url(`rules/${id}`), dto));
    this.rules.update((r) => r.map((x) => (x.id === id ? rule : x)));
  }

  async moveRule(id: number, direction: -1 | 1) {
    const ids = this.rules().map((r) => r.id);
    const index = ids.indexOf(id);
    const target = index + direction;
    if (index < 0 || target < 0 || target >= ids.length) {
      return;
    }
    [ids[index], ids[target]] = [ids[target], ids[index]];
    this.rules.update((rules) =>
      ids.map((ruleId, i) => ({ ...rules.find((r) => r.id === ruleId)!, sortOrder: i })),
    );
    await firstValueFrom(this.http.put(this.url('rules/order'), { ids }));
  }

  async deleteRule(id: number) {
    await firstValueFrom(this.http.delete(this.url(`rules/${id}`)));
    this.rules.update((r) => r.filter((x) => x.id !== id));
  }

  /** Wendet die Regeln auf alle Buchungen ohne Kategorie an. */
  async applyRules(): Promise<number> {
    const result = await firstValueFrom(
      this.http.post<{ updated: number }>(this.url('rules/apply'), {}),
    );
    return result.updated;
  }

  private url(path: string) {
    return `/api/accounts/${this.accountId()}/${path}`;
  }
}
