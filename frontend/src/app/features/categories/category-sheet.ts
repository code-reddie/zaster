import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogFooter, DialogFrame, DialogService } from '../../core/ui/dialog';
import { Icon } from '../../core/ui/icon';
import { ToastService } from '../../core/ui/toast';
import { nextColor } from './category-picker';
import { Category, CATEGORY_COLORS, descendantIds } from './category.models';
import { CategoryStore } from './category.store';

export interface CategorySheetData {
  category?: Category;
  parentId?: number | null;
}

/** Kategorie anlegen oder bearbeiten (Name, Farbe, Lage im Baum) und löschen. */
@Component({
  imports: [FormsModule, DialogFrame, DialogFooter, Button, Icon],
  template: `
    <z-dialog [title]="data.category ? 'Kategorie bearbeiten' : 'Neue Kategorie'">
      <form id="category-form" class="flex flex-col gap-4" (ngSubmit)="save()">
        <div>
          <label class="z-label" for="name">Name</label>
          <input id="name" name="name" class="z-input" [(ngModel)]="name" />
        </div>
        <div>
          <label class="z-label" for="parent">Liegt unter</label>
          <select id="parent" name="parent" class="z-input" [(ngModel)]="parentId">
            <option value="">– oberste Ebene –</option>
            @for (option of parents(); track option.id) {
              <option [value]="option.id">{{ indent(option.depth) }}{{ option.name }}</option>
            }
          </select>
        </div>
        <fieldset>
          <legend class="z-label">Farbe</legend>
          <div class="flex flex-wrap gap-2">
            @if (parentId()) {
              <button
                type="button"
                [class]="swatchClass(color() === null)"
                class="border-2 border-dashed !border-border"
                title="Wie die übergeordnete Kategorie"
                aria-label="Wie die übergeordnete Kategorie"
                (click)="color.set(null)"
              ></button>
            }
            @for (option of colors; track option) {
              <button
                type="button"
                [class]="swatchClass(color() === option)"
                [style.background-color]="option"
                [attr.aria-label]="'Farbe ' + option"
                (click)="color.set(option)"
              ></button>
            }
          </div>
        </fieldset>
        @if (error()) {
          <p class="z-error">{{ error() }}</p>
        }
      </form>
      <z-dialog-footer>
        @if (data.category) {
          <button zButton variant="ghost" type="button" class="!text-danger" (click)="delete()">
            <z-icon name="trash" class="size-4" /> Löschen
          </button>
          <span class="hidden flex-1 sm:block"></span>
        }
        <button zButton variant="secondary" type="button" (click)="ref.close()">Abbrechen</button>
        <button zButton type="submit" form="category-form" [loading]="saving()">Speichern</button>
      </z-dialog-footer>
    </z-dialog>
  `,
})
export class CategorySheet {
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
  protected readonly data = inject<CategorySheetData>(DIALOG_DATA);
  private readonly store = inject(CategoryStore);
  private readonly dialog = inject(DialogService);
  private readonly toast = inject(ToastService);

  protected readonly colors = CATEGORY_COLORS;
  protected readonly name = signal(this.data.category?.name ?? '');
  protected readonly parentId = signal<string>(
    String(this.data.category?.parentId ?? this.data.parentId ?? ''),
  );
  protected readonly color = signal<string | null>(
    this.data.category
      ? this.data.category.color
      : (this.data.parentId ?? null) !== null
        ? null
        : nextColor(this.store.categories().filter((c) => c.parentId === null).length),
  );
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Mögliche Eltern: alle außer der Kategorie selbst und ihren Unterkategorien. */
  protected readonly parents = computed(() => {
    const self = this.data.category;
    const excluded = self ? descendantIds(this.store.categories(), self.id) : new Set<number>();
    return this.store.tree().filter((c) => !excluded.has(c.id));
  });

  protected indent(depth: number) {
    return ' '.repeat(depth * 3);
  }

  protected swatchClass(selected: boolean) {
    return (
      'size-9 rounded-full transition ' +
      (selected ? 'ring-2 ring-fg ring-offset-2 ring-offset-surface' : 'hover:scale-110')
    );
  }

  protected async save() {
    if (!this.name().trim()) {
      this.error.set('Bitte gib einen Namen ein.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const dto = {
      name: this.name().trim(),
      color: this.color(),
      parentId: this.parentId() ? Number(this.parentId()) : null,
    };
    try {
      if (this.data.category) {
        await this.store.updateCategory(this.data.category.id, dto);
      } else {
        await this.store.createCategory(dto);
      }
      this.ref.close(true);
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Kategorie konnte nicht gespeichert werden.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async delete() {
    const category = this.data.category!;
    const children = descendantIds(this.store.categories(), category.id).size - 1;
    const confirmed = await this.dialog.confirm({
      title: `„${category.name}“ löschen?`,
      text:
        (children > 0
          ? `Auch ${children === 1 ? 'die Unterkategorie' : `die ${children} Unterkategorien`} und ihre Regeln werden gelöscht. `
          : 'Ihre Regeln werden mitgelöscht. ') +
        'Die Buchungen bleiben, sind danach aber wieder ohne Kategorie.',
      confirm: 'Löschen',
      danger: true,
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.store.deleteCategory(category.id);
      this.toast.success('Kategorie gelöscht.');
      this.ref.close(true);
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Kategorie konnte nicht gelöscht werden.'));
    }
  }
}
