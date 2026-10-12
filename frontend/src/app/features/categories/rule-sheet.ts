import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogFooter, DialogFrame, DialogService } from '../../core/ui/dialog';
import { Icon } from '../../core/ui/icon';
import { ToastService } from '../../core/ui/toast';
import { CategoryDot } from './category-dot';
import { CategoryPicker, CategoryPickerData } from './category-picker';
import {
  Rule,
  RuleCondition,
  RULE_DIRECTIONS,
  RULE_FIELDS,
  RuleDirection,
} from './category.models';
import { CategoryStore } from './category.store';

export interface RuleSheetData {
  rule?: Rule;
  conditions?: RuleCondition[];
  direction?: RuleDirection;
  categoryId?: number | null;
}

interface ConditionRow extends RuleCondition {
  /** Nur fürs Rendern, damit Eingabefelder beim Entfernen nicht springen. */
  key: number;
}

/**
 * Regel anlegen oder bearbeiten. Ergebnis: Zahl der Buchungen, die danach eine Kategorie
 * bekommen haben (0, wenn nicht angewendet), oder undefined bei Abbruch.
 */
@Component({
  imports: [FormsModule, DialogFrame, DialogFooter, Button, Icon, CategoryDot],
  template: `
    <z-dialog [title]="data.rule ? 'Regel bearbeiten' : 'Neue Regel'">
      <form id="rule-form" class="flex flex-col gap-4" (ngSubmit)="save()">
        <fieldset class="flex flex-col gap-2">
          <legend class="z-label">
            {{
              conditions().length > 1 ? 'Wenn alle Bedingungen passen' : 'Wenn dieser Text vorkommt'
            }}
          </legend>
          @for (condition of conditions(); track condition.key; let i = $index) {
            @if (i > 0) {
              <span class="px-1 text-xs font-medium uppercase tracking-wide text-muted">und</span>
            }
            <div class="flex flex-col gap-2 rounded-xl bg-surface-2 p-2">
              <div class="flex items-center gap-2">
                <input
                  class="z-input min-w-0 flex-1"
                  [name]="'pattern-' + condition.key"
                  [attr.aria-label]="'Text der Bedingung ' + (i + 1)"
                  placeholder="Text, z. B. Miete"
                  [ngModel]="condition.pattern"
                  (ngModelChange)="update(i, { pattern: $event })"
                />
                @if (conditions().length > 1) {
                  <button
                    zButton
                    variant="ghost"
                    size="icon"
                    type="button"
                    class="shrink-0"
                    [attr.aria-label]="'Bedingung ' + (i + 1) + ' entfernen'"
                    (click)="remove(i)"
                  >
                    <z-icon name="x" class="size-4" />
                  </button>
                }
              </div>
              <div class="flex items-center gap-2">
                <span class="shrink-0 pl-1 text-sm text-muted">in</span>
                <select
                  class="z-input min-w-0 flex-1"
                  [name]="'field-' + condition.key"
                  [attr.aria-label]="'Feld der Bedingung ' + (i + 1)"
                  [ngModel]="condition.field"
                  (ngModelChange)="update(i, { field: $event })"
                >
                  @for (option of fields; track option.value) {
                    <option [value]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>
            </div>
          }
          <div class="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
            <p class="z-hint !mt-0">
              Groß- und Kleinschreibung spielt keine Rolle, ein Teil des Wortes reicht.
            </p>
            @if (conditions().length < maxConditions) {
              <button zButton variant="ghost" size="sm" type="button" (click)="add()">
                <z-icon name="plus" class="size-4" /> Bedingung hinzufügen
              </button>
            }
          </div>
        </fieldset>
        <div>
          <span class="z-label" id="direction">bei</span>
          <div
            class="grid grid-cols-3 gap-1 rounded-xl bg-surface-2 p-1"
            role="radiogroup"
            aria-labelledby="direction"
          >
            @for (option of directions; track option.value) {
              <button
                type="button"
                role="radio"
                [attr.aria-checked]="direction() === option.value"
                [class]="segment(direction() === option.value)"
                (click)="direction.set(option.value)"
              >
                {{ option.label }}
              </button>
            }
          </div>
        </div>
        <div>
          <span class="z-label">dann Kategorie</span>
          <button
            type="button"
            class="z-input flex items-center gap-2.5 text-left"
            (click)="pickCategory()"
          >
            <z-category-dot [color]="category()?.effectiveColor" [empty]="!category()" />
            <span class="flex-1 truncate" [class.text-muted]="!category()">{{
              category()?.path ?? 'Kategorie wählen'
            }}</span>
            <z-icon name="chevron-down" class="size-4 text-muted" />
          </button>
        </div>
        <label class="flex items-center gap-3">
          <input type="checkbox" name="apply" class="z-checkbox" [(ngModel)]="apply" />
          <span class="text-sm">Danach auf alle Buchungen ohne Kategorie anwenden</span>
        </label>
        @if (error()) {
          <p class="z-error">{{ error() }}</p>
        }
      </form>
      <z-dialog-footer>
        @if (data.rule) {
          <button zButton variant="ghost" type="button" class="!text-danger" (click)="delete()">
            <z-icon name="trash" class="size-4" /> Löschen
          </button>
          <span class="hidden flex-1 sm:block"></span>
        }
        <button zButton variant="secondary" type="button" (click)="ref.close()">Abbrechen</button>
        <button zButton type="submit" form="rule-form" [loading]="saving()">Speichern</button>
      </z-dialog-footer>
    </z-dialog>
  `,
})
export class RuleSheet {
  protected readonly ref = inject<DialogRef<number>>(DialogRef);
  protected readonly data = inject<RuleSheetData>(DIALOG_DATA);
  private readonly store = inject(CategoryStore);
  private readonly dialog = inject(DialogService);
  private readonly toast = inject(ToastService);

  protected readonly fields = RULE_FIELDS;
  /** Höchstzahl wie im Backend. */
  protected readonly maxConditions = 10;
  private nextKey = 0;
  protected readonly conditions = signal<ConditionRow[]>(
    (this.data.rule?.conditions ?? this.data.conditions ?? [{ field: 'Alle', pattern: '' }]).map(
      (c) => this.row(c),
    ),
  );
  protected readonly directions = RULE_DIRECTIONS;
  protected readonly direction = signal<RuleDirection>(
    this.data.rule?.direction ?? this.data.direction ?? 'Beide',
  );
  protected readonly categoryId = signal<number | null>(
    this.data.rule?.categoryId ?? this.data.categoryId ?? null,
  );
  protected readonly apply = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly category = computed(() => {
    const id = this.categoryId();
    return id === null ? null : (this.store.byId().get(id) ?? null);
  });

  protected segment(active: boolean) {
    return (
      'h-9 rounded-lg text-sm font-medium transition ' +
      (active ? 'bg-surface shadow-card' : 'text-muted hover:text-fg')
    );
  }

  private row(condition: RuleCondition): ConditionRow {
    return { ...condition, key: this.nextKey++ };
  }

  protected update(index: number, change: Partial<RuleCondition>) {
    this.conditions.update((rows) => rows.map((r, i) => (i === index ? { ...r, ...change } : r)));
  }

  protected add() {
    this.conditions.update((rows) => [
      ...rows,
      this.row({ field: 'Verwendungszweck', pattern: '' }),
    ]);
  }

  protected remove(index: number) {
    this.conditions.update((rows) => rows.filter((_, i) => i !== index));
  }

  protected async pickCategory() {
    const id = await this.dialog.result<number | null, CategoryPickerData>(CategoryPicker, {
      selected: this.categoryId(),
      title: 'Kategorie für die Regel',
    });
    if (id !== undefined && id !== null) {
      this.categoryId.set(id);
    }
  }

  protected async save() {
    const categoryId = this.categoryId();
    const conditions = this.conditions().map((c) => ({
      field: c.field,
      pattern: c.pattern.trim(),
    }));
    if (conditions.some((c) => !c.pattern)) {
      this.error.set(
        conditions.length > 1
          ? 'Bitte gib in jeder Bedingung einen Text ein.'
          : 'Bitte gib einen Text ein.',
      );
      return;
    }
    if (categoryId === null) {
      this.error.set('Bitte wähle eine Kategorie.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    try {
      const dto = {
        conditions,
        direction: this.direction(),
        categoryId,
      };
      if (this.data.rule) {
        await this.store.updateRule(this.data.rule.id, dto);
      } else {
        await this.store.createRule(dto);
      }
      const updated = this.apply() ? await this.store.applyRules() : 0;
      this.toast.success(
        this.apply()
          ? `Regel gespeichert, ${updated} ${updated === 1 ? 'Buchung' : 'Buchungen'} zugeordnet.`
          : 'Regel gespeichert.',
      );
      this.ref.close(updated);
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Regel konnte nicht gespeichert werden.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async delete() {
    try {
      await this.store.deleteRule(this.data.rule!.id);
      this.toast.success('Regel gelöscht.');
      this.ref.close(0);
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Regel konnte nicht gelöscht werden.'));
    }
  }
}
