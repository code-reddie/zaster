import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { errorMessage } from '../../core/errors';
import { DialogFrame } from '../../core/ui/dialog';
import { Icon } from '../../core/ui/icon';
import { CategoryDot } from './category-dot';
import { CATEGORY_COLORS } from './category.models';
import { CategoryStore } from './category.store';

export interface CategoryPickerData {
  title?: string;
  subtitle?: string;
  selected: number | null;
  /** „Ohne Kategorie“ anbieten (zum Entfernen). */
  allowNone?: boolean;
}

/**
 * Kategorie aus dem Baum wählen, mit Suche und „Neue Kategorie“. Ergebnis: Id, null für
 * „Ohne Kategorie“ oder undefined bei Abbruch.
 */
@Component({
  imports: [FormsModule, DialogFrame, Icon, CategoryDot],
  template: `
    <z-dialog [title]="data.title ?? 'Kategorie wählen'" [subtitle]="data.subtitle">
      <div class="relative mb-3">
        <z-icon
          name="search"
          class="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted"
        />
        <input
          class="z-input pl-9"
          placeholder="Suchen oder neu anlegen"
          [(ngModel)]="query"
          (keydown.enter)="enter()"
        />
      </div>

      <div class="-mx-2 flex flex-col">
        @if (data.allowNone && !query()) {
          <button
            type="button"
            [class]="rowClass(data.selected === null)"
            (click)="ref.close(null)"
          >
            <z-category-dot [empty]="true" />
            <span class="flex-1 text-muted">Ohne Kategorie</span>
          </button>
        }
        @for (category of filtered(); track category.id) {
          <button
            type="button"
            [class]="rowClass(category.id === data.selected)"
            [style.padding-left.rem]="query() ? 0.5 : 0.5 + category.depth * 1.25"
            (click)="ref.close(category.id)"
          >
            <z-category-dot [color]="category.effectiveColor" />
            <span class="min-w-0 flex-1 truncate">{{
              query() ? category.path : category.name
            }}</span>
            @if (category.id === data.selected) {
              <z-icon name="check" class="size-4 text-accent" />
            }
          </button>
        }
        @if (filtered().length === 0 && !query()) {
          <p class="px-2 py-6 text-center text-sm text-muted">
            Dieses Konto hat noch keine Kategorien. Tippe oben einen Namen ein, um die erste
            anzulegen.
          </p>
        }
        @if (query().trim() && !exactMatch()) {
          <button
            type="button"
            class="mt-1 flex items-center gap-3 rounded-xl px-2 py-2.5 text-left text-accent hover:bg-surface-2"
            [disabled]="creating()"
            (click)="create()"
          >
            <z-icon name="plus" class="size-4" />
            <span>„{{ query().trim() }}“ als neue Kategorie anlegen</span>
          </button>
        }
        @if (error()) {
          <p class="z-error mx-2 mt-2">{{ error() }}</p>
        }
      </div>
    </z-dialog>
  `,
})
export class CategoryPicker {
  protected readonly ref = inject<DialogRef<number | null>>(DialogRef);
  protected readonly data = inject<CategoryPickerData>(DIALOG_DATA);
  private readonly store = inject(CategoryStore);

  protected readonly query = signal('');
  protected readonly creating = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLocaleLowerCase('de');
    const tree = this.store.tree();
    return q ? tree.filter((c) => c.path.toLocaleLowerCase('de').includes(q)) : tree;
  });

  protected readonly exactMatch = computed(() => {
    const q = this.query().trim().toLocaleLowerCase('de');
    return this.store.tree().some((c) => c.name.toLocaleLowerCase('de') === q);
  });

  protected rowClass(selected: boolean) {
    return (
      'flex items-center gap-3 rounded-xl px-2 py-2.5 text-left hover:bg-surface-2 ' +
      (selected ? 'bg-surface-2 font-medium' : '')
    );
  }

  protected enter() {
    const matches = this.filtered();
    if (matches.length === 1) {
      this.ref.close(matches[0].id);
    } else if (this.query().trim() && !this.exactMatch()) {
      this.create();
    }
  }

  protected async create() {
    this.creating.set(true);
    this.error.set(null);
    try {
      const category = await this.store.createCategory({
        name: this.query().trim(),
        color: nextColor(this.store.categories().filter((c) => c.parentId === null).length),
        parentId: null,
      });
      this.ref.close(category.id);
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Kategorie konnte nicht angelegt werden.'));
    } finally {
      this.creating.set(false);
    }
  }
}

/**
 * Farbe für die n-te neue Kategorie. Springt jeweils 7 Plätze weiter, damit aufeinanderfolgende
 * Kategorien nicht fast gleich aussehen (7 und 24 sind teilerfremd, also kommt jede Farbe dran).
 */
export function nextColor(index: number): string {
  return CATEGORY_COLORS[(index * 7) % CATEGORY_COLORS.length];
}
