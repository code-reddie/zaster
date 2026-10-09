import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import {
  CategorizationRule,
  Category,
  CreateCategorizationRule,
  CreateCategory,
} from './category.models';
import { CategoryService } from './category.service';

type CategoryState = {
  categories: Category[];
  rules: CategorizationRule[];
  loading: boolean;
  error: string | null;
};

const initialState: CategoryState = {
  categories: [],
  rules: [],
  loading: false,
  error: null,
};

export const CategoryStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ categories }) => ({
    categoriesById: computed(() => new Map(categories().map((c) => [c.id, c]))),
  })),
  withMethods((store, categoryService = inject(CategoryService)) => ({
    async loadCategories() {
      patchState(store, { loading: true, error: null });
      try {
        const [categories, rules] = await Promise.all([
          categoryService.getCategories(),
          categoryService.getRules(),
        ]);
        patchState(store, { categories, rules });
      } catch {
        patchState(store, { error: 'Kategorien konnten nicht geladen werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    async createCategory(dto: CreateCategory) {
      patchState(store, { loading: true, error: null });
      try {
        const category = await categoryService.createCategory(dto);
        const categories = [...store.categories(), category].sort((a, b) =>
          a.name.localeCompare(b.name, 'de'),
        );
        patchState(store, { categories });
      } catch {
        patchState(store, { error: 'Kategorie konnte nicht erstellt werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    async deleteCategory(id: number) {
      patchState(store, { loading: true, error: null });
      try {
        await categoryService.deleteCategory(id);
        patchState(store, {
          categories: store.categories().filter((c) => c.id !== id),
          rules: store.rules().filter((r) => r.categoryId !== id),
        });
      } catch {
        patchState(store, { error: 'Kategorie konnte nicht gelöscht werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    async createRule(dto: CreateCategorizationRule) {
      patchState(store, { loading: true, error: null });
      try {
        const rule = await categoryService.createRule(dto);
        patchState(store, { rules: [...store.rules(), rule] });
      } catch {
        patchState(store, { error: 'Regel konnte nicht erstellt werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    async deleteRule(id: number) {
      patchState(store, { loading: true, error: null });
      try {
        await categoryService.deleteRule(id);
        patchState(store, { rules: store.rules().filter((r) => r.id !== id) });
      } catch {
        patchState(store, { error: 'Regel konnte nicht gelöscht werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    /** Wendet alle Regeln auf unkategorisierte Buchungen an und gibt die Anzahl zurück. */
    async applyRules(): Promise<number | null> {
      patchState(store, { loading: true, error: null });
      try {
        const result = await categoryService.applyRules();
        return result.updated;
      } catch {
        patchState(store, { error: 'Regeln konnten nicht angewendet werden.' });
        return null;
      } finally {
        patchState(store, { loading: false });
      }
    },
  })),
);
