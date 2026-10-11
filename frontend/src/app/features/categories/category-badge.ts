import { Component, computed, input } from '@angular/core';
import { AmountPipe } from '../../shared/format';
import { CategoryNode } from './category.models';

/**
 * Kategorie als Badge in der Farbe der Kategorie, optional mit Teilbetrag (ab 640 px sichtbar,
 * sonst im Tooltip). Ohne Kategorie ein gestricheltes Badge „Ohne Kategorie“.
 */
@Component({
  selector: 'z-category-badge',
  imports: [AmountPipe],
  host: {
    class:
      'inline-flex h-6 min-w-0 max-w-44 items-center gap-1 rounded-full px-2.5 text-xs font-medium',
    '[style]': 'style()',
    '[attr.title]': 'title()',
  },
  template: `
    <span class="truncate">{{ category()?.name ?? 'Ohne Kategorie' }}</span>
    @if (amount() !== null && amount() !== undefined) {
      <span class="amount hidden shrink-0 opacity-75 sm:inline">{{ amount() | amount }}</span>
    }
  `,
})
export class CategoryBadge {
  readonly category = input<CategoryNode | null | undefined>();
  /** Teilbetrag ohne Vorzeichen, nur bei aufgeteilten Buchungen. */
  readonly amount = input<number | null>();

  protected readonly style = computed(() => {
    if (!this.category()) {
      return 'border: 1px dashed var(--z-border); color: var(--z-muted)';
    }
    const color = this.category()!.effectiveColor ?? 'var(--z-muted)';
    return (
      `background-color: color-mix(in srgb, ${color} 15%, transparent); ` +
      `color: color-mix(in srgb, ${color} 70%, var(--z-fg))`
    );
  });

  protected readonly title = computed(() => {
    const category = this.category();
    if (!category) {
      return null;
    }
    const amount = this.amount();
    return amount === null || amount === undefined
      ? category.path
      : `${category.path}: ${amount.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' })}`;
  });
}
