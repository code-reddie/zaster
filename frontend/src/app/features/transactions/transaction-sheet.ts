import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, computed, inject, signal } from '@angular/core';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogFooter, DialogFrame, DialogService } from '../../core/ui/dialog';
import { Icon } from '../../core/ui/icon';
import { ToastService } from '../../core/ui/toast';
import { AmountPipe, dayLabel } from '../../shared/format';
import { CategoryDot } from '../categories/category-dot';
import { CategoryPicker, CategoryPickerData } from '../categories/category-picker';
import { CategoryStore } from '../categories/category.store';
import { RuleSheet, RuleSheetData } from '../categories/rule-sheet';
import { BemerkungInput } from './bemerkung-input';
import { SplitSheet } from './split-sheet';
import { singleCategoryId, SOURCE_LABELS, Transaction } from './transaction.models';
import { TransactionsService } from './transactions.service';

export type TransactionSheetResult =
  | { updated: Transaction }
  | { deleted: number }
  /** Regeln haben sich geändert und wurden angewendet: Liste neu laden. */
  | { reload: true };

/** Vorbelegung für eine neue Regel aus einer Buchung. */
export function ruleFromTransaction(t: Transaction): RuleSheetData {
  return {
    conditions: [
      {
        pattern: t.auftraggeber || t.buchungstext,
        field: t.auftraggeber ? 'Auftraggeber' : 'Buchungstext',
      },
    ],
    direction: t.betrag > 0 ? 'Eingang' : t.betrag < 0 ? 'Ausgang' : 'Beide',
    categoryId: singleCategoryId(t),
  };
}

/**
 * Details einer Buchung: Kategorie setzen oder aufteilen, abhaken, Bemerkung eintragen, Regel daraus
 * machen, löschen.
 */
@Component({
  imports: [DialogFrame, DialogFooter, Button, Icon, AmountPipe, CategoryDot, BemerkungInput],
  template: `
    <z-dialog title="Buchung" [subtitle]="day()">
      <div class="mb-5 text-center">
        <p
          class="amount text-3xl font-bold tracking-tight"
          [class.text-positive]="transaction().betrag > 0"
        >
          {{ transaction().betrag | amount: true }}
        </p>
        <p class="mt-1 font-medium">{{ transaction().auftraggeber || '–' }}</p>
      </div>

      @if (parts().length > 1) {
        <button
          type="button"
          class="w-full rounded-xl border border-border px-3 py-3 text-left hover:bg-surface-2"
          (click)="split()"
        >
          <span class="flex items-center gap-2 text-xs text-muted">
            <span class="flex-1">Aufgeteilt auf {{ parts().length }} Kategorien</span>
            <span class="text-accent">Ändern</span>
          </span>
          @for (part of parts(); track part.categoryId) {
            <span class="mt-2 flex items-center gap-3">
              <z-category-dot [color]="part.category?.effectiveColor" />
              <span class="min-w-0 flex-1 truncate font-medium">{{
                part.category?.path ?? '–'
              }}</span>
              <span class="amount shrink-0">{{ part.betrag | amount }}</span>
            </span>
          }
        </button>
        <button
          type="button"
          class="mt-1 flex items-center gap-2 rounded-xl px-2 py-2 text-sm text-muted hover:bg-surface-2 hover:text-fg"
          (click)="pickCategory()"
        >
          <z-icon name="tag" class="size-4" /> Eine Kategorie für den ganzen Betrag
        </button>
      } @else {
        <button
          type="button"
          class="flex w-full items-center gap-3 rounded-xl border border-border px-3 py-3 text-left hover:bg-surface-2"
          (click)="pickCategory()"
        >
          <z-category-dot [color]="category()?.effectiveColor" [empty]="!category()" />
          <span class="min-w-0 flex-1">
            <span class="block text-xs text-muted">Kategorie</span>
            <span class="block truncate font-medium">{{
              category()?.path ?? 'Ohne Kategorie'
            }}</span>
          </span>
          <z-icon name="chevron-right" class="size-4 text-muted" />
        </button>
        @if (transaction().betrag !== 0) {
          <button
            type="button"
            class="mt-1 flex items-center gap-2 rounded-xl px-2 py-2 text-sm text-accent hover:bg-surface-2"
            (click)="split()"
          >
            <z-icon name="split" class="size-4" /> Auf mehrere Kategorien aufteilen
          </button>
        }
      }

      @if (parts().length === 0) {
        <label class="mt-2 flex items-center gap-3 rounded-xl border border-border px-3 py-3">
          <input
            type="checkbox"
            class="z-checkbox"
            [checked]="transaction().reviewed"
            (change)="toggleReviewed()"
          />
          <span class="flex-1">
            <span class="block font-medium">Geprüft</span>
            <span class="block text-xs text-muted"
              >Bleibt ohne Kategorie, taucht aber nicht mehr unter „Ohne Kategorie“ auf.</span
            >
          </span>
        </label>
      }

      <div class="mt-4">
        <z-bemerkung-input inputId="sheet-bemerkung" [(value)]="note" />
        @if (noteChanged()) {
          <div class="mt-2 flex justify-end gap-2">
            <button zButton variant="ghost" size="sm" type="button" (click)="resetNote()">
              Verwerfen
            </button>
            <button zButton size="sm" type="button" [loading]="savingNote()" (click)="saveNote()">
              Bemerkung speichern
            </button>
          </div>
        }
      </div>

      <dl class="mt-5 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2.5 text-sm">
        <dt class="text-muted">Buchungstext</dt>
        <dd>{{ transaction().buchungstext || '–' }}</dd>
        <dt class="text-muted">Verwendungszweck</dt>
        <dd class="break-words">{{ transaction().verwendungszweck || '–' }}</dd>
        <dt class="text-muted">Buchungstag</dt>
        <dd>{{ transaction().buchung.split('-').reverse().join('.') }}</dd>
        <dt class="text-muted">Valuta</dt>
        <dd>{{ transaction().valuta.split('-').reverse().join('.') }}</dd>
        @if (transaction().saldo !== null) {
          <dt class="text-muted">Kontostand danach</dt>
          <dd class="amount">{{ transaction().saldo | amount }}</dd>
        }
        <dt class="text-muted">Herkunft</dt>
        <dd>{{ source() }}</dd>
      </dl>

      @if (error()) {
        <p class="z-error mt-4">{{ error() }}</p>
      }

      <z-dialog-footer>
        <button zButton variant="ghost" type="button" class="!text-danger" (click)="delete()">
          <z-icon name="trash" class="size-4" /> Löschen
        </button>
        <span class="hidden flex-1 sm:block"></span>
        <button zButton variant="secondary" type="button" (click)="createRule()">
          <z-icon name="zap" class="size-4" /> Regel daraus machen
        </button>
      </z-dialog-footer>
    </z-dialog>
  `,
})
export class TransactionSheet {
  private readonly ref = inject<DialogRef<TransactionSheetResult>>(DialogRef);
  private readonly service = inject(TransactionsService);
  private readonly categories = inject(CategoryStore);
  private readonly dialog = inject(DialogService);
  private readonly toast = inject(ToastService);

  protected readonly transaction = signal(inject<Transaction>(DIALOG_DATA));
  protected readonly error = signal<string | null>(null);
  protected readonly note = signal(this.transaction().bemerkung ?? '');
  protected readonly savingNote = signal(false);
  protected readonly noteChanged = computed(
    () => this.note().trim() !== (this.transaction().bemerkung ?? ''),
  );

  protected readonly parts = computed(() =>
    this.transaction().splits.map((s) => ({
      categoryId: s.categoryId,
      betrag: Math.abs(s.betrag),
      category: this.categories.byId().get(s.categoryId),
    })),
  );
  protected readonly category = computed(() =>
    this.parts().length === 1 ? (this.parts()[0].category ?? null) : null,
  );
  protected readonly day = computed(() => dayLabel(this.transaction().buchung));
  protected readonly source = computed(() => SOURCE_LABELS[this.transaction().source]);

  protected async pickCategory() {
    const categoryId = await this.dialog.result<number | null, CategoryPickerData>(CategoryPicker, {
      selected: singleCategoryId(this.transaction()),
      allowNone: true,
      subtitle: this.transaction().auftraggeber,
    });
    if (
      categoryId === undefined ||
      (categoryId === singleCategoryId(this.transaction()) && this.transaction().splits.length <= 1)
    ) {
      return;
    }
    await this.save(() =>
      this.service.setCategory(this.transaction().accountId, this.transaction().id, categoryId),
    );
  }

  protected async split() {
    const updated = await this.dialog.result<Transaction, Transaction>(
      SplitSheet,
      this.transaction(),
    );
    if (updated) {
      this.transaction.set(updated);
      this.ref.close({ updated });
    }
  }

  protected async toggleReviewed() {
    await this.save(() =>
      this.service.setReviewed(
        this.transaction().accountId,
        this.transaction().id,
        !this.transaction().reviewed,
      ),
    );
  }

  protected resetNote() {
    this.note.set(this.transaction().bemerkung ?? '');
  }

  protected async saveNote() {
    this.savingNote.set(true);
    try {
      await this.save(() =>
        this.service.setBemerkung(
          this.transaction().accountId,
          this.transaction().id,
          this.note().trim(),
        ),
      );
    } finally {
      this.savingNote.set(false);
    }
  }

  protected async createRule() {
    const applied = await this.dialog.result<number, RuleSheetData>(
      RuleSheet,
      ruleFromTransaction(this.transaction()),
    );
    if (applied !== undefined) {
      this.ref.close({ reload: true });
    }
  }

  protected async delete() {
    const confirmed = await this.dialog.confirm({
      title: 'Buchung löschen?',
      text: 'Kommt sie beim nächsten Abruf oder CSV-Import wieder, legt Zaster sie neu an.',
      confirm: 'Löschen',
      danger: true,
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.service.delete(this.transaction().accountId, this.transaction().id);
      this.toast.success('Buchung gelöscht.');
      this.ref.close({ deleted: this.transaction().id });
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Buchung konnte nicht gelöscht werden.'));
    }
  }

  private async save(action: () => Promise<Transaction>) {
    this.error.set(null);
    try {
      const updated = await action();
      this.transaction.set(updated);
      this.ref.close({ updated });
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Buchung konnte nicht gespeichert werden.'));
    }
  }
}
