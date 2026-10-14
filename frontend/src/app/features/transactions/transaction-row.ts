import { Component, computed, inject, input } from '@angular/core';
import { Icon } from '../../core/ui/icon';
import { AmountPipe } from '../../shared/format';
import { CategoryBadge } from '../categories/category-badge';
import { CategoryStore } from '../categories/category.store';
import { Transaction } from './transaction.models';

/** Eine Buchung in Listen: Kürzel, Auftraggeber, Bemerkung, Kategorien als Badges und Betrag. */
@Component({
  selector: 'z-transaction-row',
  imports: [AmountPipe, CategoryBadge, Icon],
  host: { class: 'flex min-w-0 items-center gap-3' },
  template: `
    <span
      class="grid size-10 shrink-0 place-items-center rounded-full text-sm font-semibold"
      [style]="avatarStyle()"
      >{{ initial() }}</span
    >
    <span class="min-w-0 flex-1">
      <span class="block truncate font-medium">{{
        transaction().auftraggeber || transaction().buchungstext
      }}</span>
      @if (showBemerkung() && transaction().bemerkung) {
        <span class="mt-0.5 flex min-w-0 items-center gap-1.5 text-sm">
          <z-icon name="note" class="size-3.5 text-accent" />
          <span class="min-w-0 truncate" [title]="transaction().bemerkung">{{
            transaction().bemerkung
          }}</span>
        </span>
      }
      <span class="mt-1 flex min-w-0 items-center gap-1.5 text-sm text-muted">
        @for (part of parts(); track part.category.id) {
          <z-category-badge
            [category]="part.category"
            [amount]="parts().length > 1 ? part.betrag : null"
          />
        } @empty {
          <z-category-badge class="shrink-0" />
          @if (transaction().reviewed) {
            <span class="shrink-0">geprüft</span>
          }
        }
        @if (showPurpose() && transaction().verwendungszweck) {
          <span class="hidden min-w-0 truncate sm:inline">{{
            transaction().verwendungszweck
          }}</span>
        }
      </span>
    </span>
    <span
      class="amount shrink-0 text-right font-semibold"
      [class.text-positive]="transaction().betrag > 0"
      >{{ transaction().betrag | amount: true }}</span
    >
  `,
})
export class TransactionRow {
  private readonly store = inject(CategoryStore);

  readonly transaction = input.required<Transaction>();
  readonly showPurpose = input(true);
  readonly showBemerkung = input(true);

  /** Teilbeträge mit Kategorie, größter Teil zuerst. */
  protected readonly parts = computed(() =>
    this.transaction().splits.flatMap((s) => {
      const category = this.store.byId().get(s.categoryId);
      return category ? [{ category, betrag: Math.abs(s.betrag) }] : [];
    }),
  );

  protected readonly initial = computed(() => {
    const name = this.transaction().auftraggeber || this.transaction().buchungstext || '?';
    return name.trim().charAt(0).toUpperCase();
  });

  protected readonly avatarStyle = computed(() => {
    const color = this.parts()[0]?.category.effectiveColor;
    return color
      ? `background-color: color-mix(in srgb, ${color} 16%, transparent); color: color-mix(in srgb, ${color} 70%, var(--z-fg))`
      : 'border: 1.5px dashed var(--z-border); color: var(--z-muted)';
  });
}
