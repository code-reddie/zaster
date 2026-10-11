import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogFooter, DialogFrame, DialogService } from '../../core/ui/dialog';
import { Icon } from '../../core/ui/icon';
import { AmountPipe } from '../../shared/format';
import { CategoryDot } from '../categories/category-dot';
import { CategoryPicker, CategoryPickerData } from '../categories/category-picker';
import { CategoryStore } from '../categories/category.store';
import { parseAmount } from './create-transaction-sheet';
import { singleCategoryId, Transaction } from './transaction.models';
import { TransactionsService } from './transactions.service';

interface Part {
  categoryId: number | null;
  /** Eingabe ohne Vorzeichen; beim ersten Teil ungenutzt, er bekommt den Rest. */
  amount: string;
}

/**
 * Buchung auf mehrere Kategorien aufteilen, z. B. Lebensmittel und eine Bohrmaschine im selben
 * Einkauf. Der erste Teil bekommt immer den Rest, so ergeben die Teile zusammen den Betrag.
 * Ergebnis: die gespeicherte Buchung oder undefined bei Abbruch.
 */
@Component({
  imports: [FormsModule, DialogFrame, DialogFooter, Button, Icon, AmountPipe, CategoryDot],
  template: `
    <z-dialog title="Aufteilen" [subtitle]="subtitle()">
      <p class="mb-4 text-sm text-muted">
        Gib für jede weitere Kategorie ihren Teil ein. Die erste Kategorie bekommt den Rest.
      </p>

      <div class="flex flex-col gap-2">
        @for (part of parts(); track $index; let i = $index) {
          <div class="flex items-center gap-2">
            <button
              type="button"
              class="flex min-w-0 flex-1 items-center gap-2.5 rounded-xl border border-border px-3 py-2.5 text-left hover:bg-surface-2"
              (click)="pick(i)"
            >
              <z-category-dot
                [color]="category(part.categoryId)?.effectiveColor"
                [empty]="part.categoryId === null"
              />
              <span class="min-w-0 flex-1 truncate" [class.text-muted]="part.categoryId === null">{{
                category(part.categoryId)?.path ?? 'Kategorie wählen'
              }}</span>
            </button>
            @if (i === 0) {
              <span
                class="amount w-28 shrink-0 px-3 text-right font-medium"
                [class.text-danger]="restCents() <= 0"
                title="Rest"
                >{{ restCents() / 100 | amount }}</span
              >
              <span class="size-9 shrink-0"></span>
            } @else {
              <input
                class="z-input amount w-28 shrink-0 text-right"
                inputmode="decimal"
                placeholder="0,00"
                [attr.aria-label]="'Betrag ' + (i + 1)"
                [ngModel]="part.amount"
                (ngModelChange)="setAmount(i, $event)"
              />
              <button
                type="button"
                class="grid size-9 shrink-0 place-items-center rounded-full text-muted hover:bg-surface-2 hover:text-fg"
                aria-label="Teil entfernen"
                (click)="remove(i)"
              >
                <z-icon name="x" class="size-4" />
              </button>
            }
          </div>
        }
      </div>

      <button
        type="button"
        class="mt-2 flex items-center gap-2 rounded-xl px-2 py-2 text-accent hover:bg-surface-2"
        (click)="add()"
      >
        <z-icon name="plus" class="size-4" /> Weitere Kategorie
      </button>

      <div class="mt-4 flex justify-between border-t border-border pt-3 text-sm">
        <span class="text-muted">Buchung</span>
        <span class="amount font-semibold">{{ totalCents / 100 | amount }}</span>
      </div>

      @if (problem(); as p) {
        <p class="z-hint mt-3">{{ p }}</p>
      }
      @if (error()) {
        <p class="z-error mt-3">{{ error() }}</p>
      }

      <z-dialog-footer>
        <button zButton variant="secondary" type="button" (click)="ref.close()">Abbrechen</button>
        <button zButton type="button" [disabled]="!!problem() || saving()" (click)="save()">
          Speichern
        </button>
      </z-dialog-footer>
    </z-dialog>
  `,
})
export class SplitSheet {
  protected readonly ref = inject<DialogRef<Transaction>>(DialogRef);
  private readonly transaction = inject<Transaction>(DIALOG_DATA);
  private readonly service = inject(TransactionsService);
  private readonly store = inject(CategoryStore);
  private readonly dialog = inject(DialogService);

  protected readonly totalCents = Math.round(Math.abs(this.transaction.betrag) * 100);
  protected readonly parts = signal<Part[]>(this.initialParts());
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly subtitle = computed(() => this.transaction.auftraggeber);

  private readonly cents = computed(() =>
    this.parts()
      .slice(1)
      .map((p) => {
        const value = parseAmount(p.amount);
        return value === null ? null : Math.round(value * 100);
      }),
  );

  protected readonly restCents = computed(
    () => this.totalCents - this.cents().reduce<number>((sum, c) => sum + (c ?? 0), 0),
  );

  protected readonly problem = computed(() => {
    const parts = this.parts();
    if (parts.some((p) => p.categoryId === null)) {
      return 'Wähle für jeden Teil eine Kategorie.';
    }
    if (new Set(parts.map((p) => p.categoryId)).size !== parts.length) {
      return 'Jede Kategorie darf nur einmal vorkommen.';
    }
    if (this.cents().some((c) => c === null || c <= 0)) {
      return 'Gib für jeden weiteren Teil einen Betrag über 0 € ein.';
    }
    if (this.restCents() <= 0) {
      return 'Die Teile sind zusammen größer als die Buchung, für die erste Kategorie bleibt nichts.';
    }
    return null;
  });

  protected category(id: number | null) {
    return id === null ? null : (this.store.byId().get(id) ?? null);
  }

  protected async pick(index: number) {
    const categoryId = await this.dialog.result<number | null, CategoryPickerData>(CategoryPicker, {
      selected: this.parts()[index].categoryId,
      subtitle: this.transaction.auftraggeber,
    });
    if (categoryId !== undefined && categoryId !== null) {
      this.update(index, { categoryId });
    }
  }

  protected setAmount(index: number, amount: string) {
    this.update(index, { amount });
  }

  protected add() {
    this.parts.update((parts) => [...parts, { categoryId: null, amount: '' }]);
  }

  protected remove(index: number) {
    this.parts.update((parts) => parts.filter((_, i) => i !== index));
  }

  protected async save() {
    if (this.problem()) {
      return;
    }
    const sign = this.transaction.betrag < 0 ? -1 : 1;
    const cents = [this.restCents(), ...this.cents().map((c) => c ?? 0)];
    const splits = this.parts().map((p, i) => ({
      categoryId: p.categoryId!,
      betrag: (sign * cents[i]) / 100,
    }));
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.service.setSplits(
        this.transaction.accountId,
        this.transaction.id,
        splits,
      );
      this.ref.close(updated);
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Aufteilung konnte nicht gespeichert werden.'));
    } finally {
      this.saving.set(false);
    }
  }

  private update(index: number, change: Partial<Part>) {
    this.parts.update((parts) => parts.map((p, i) => (i === index ? { ...p, ...change } : p)));
  }

  /** Bestehende Aufteilung übernehmen, sonst die bisherige Kategorie plus eine leere Zeile. */
  private initialParts(): Part[] {
    const splits = this.transaction.splits;
    if (splits.length > 1) {
      return splits.map((s) => ({
        categoryId: s.categoryId,
        amount: Math.abs(s.betrag).toFixed(2).replace('.', ','),
      }));
    }
    return [
      { categoryId: singleCategoryId(this.transaction), amount: '' },
      { categoryId: null, amount: '' },
    ];
  }
}
