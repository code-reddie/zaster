import { Component, computed, inject, input } from '@angular/core';
import { AmountPipe } from '../../shared/format';
import { CategoryStore } from '../categories/category.store';
import { Transaction } from './transaction.models';

/** Eine Buchung in Listen: Kürzel, Auftraggeber, Kategorie und Betrag. */
@Component({
  selector: 'z-transaction-row',
  imports: [AmountPipe],
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
      <span class="flex min-w-0 items-center gap-1.5 text-sm text-muted">
        @if (category(); as c) {
          <span class="truncate">{{ c.path }}</span>
        } @else {
          <span class="shrink-0 italic">Ohne Kategorie</span>
          @if (transaction().reviewed) {
            <span class="shrink-0">· geprüft</span>
          }
        }
        @if (showPurpose() && transaction().verwendungszweck) {
          <span class="hidden truncate sm:inline">· {{ transaction().verwendungszweck }}</span>
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
  private readonly categories = inject(CategoryStore);

  readonly transaction = input.required<Transaction>();
  readonly showPurpose = input(true);

  protected readonly category = computed(() => {
    const id = this.transaction().categoryId;
    return id === null ? null : (this.categories.byId().get(id) ?? null);
  });

  protected readonly initial = computed(() => {
    const name = this.transaction().auftraggeber || this.transaction().buchungstext || '?';
    return name.trim().charAt(0).toUpperCase();
  });

  protected readonly avatarStyle = computed(() => {
    const color = this.category()?.effectiveColor;
    return color
      ? `background-color: color-mix(in srgb, ${color} 16%, transparent); color: ${color}`
      : 'border: 1.5px dashed var(--z-border); color: var(--z-muted)';
  });
}
