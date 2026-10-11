import { Component, computed, input } from '@angular/core';
import { Spinner } from './spinner';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

const BASE =
  'relative inline-flex items-center justify-center gap-2 rounded-xl font-medium whitespace-nowrap transition ' +
  'select-none active:scale-[0.98] disabled:pointer-events-none disabled:opacity-50';

const VARIANTS: Record<ButtonVariant, string> = {
  primary: 'bg-accent text-accent-fg hover:bg-accent-hover shadow-card',
  secondary: 'border border-border bg-surface text-fg hover:bg-surface-2 shadow-card',
  ghost: 'text-fg hover:bg-surface-2',
  danger: 'bg-danger text-white hover:opacity-90',
};

const SIZES = {
  sm: 'h-8 px-3 text-sm',
  md: 'h-10 px-4 text-sm',
  lg: 'h-12 px-5 text-base',
  icon: 'size-10',
  'icon-sm': 'size-8',
} as const;

/**
 * Taste im Zaster-Stil. Mit `loading` zeigt sie einen Kreisel und ist gesperrt.
 *
 * `<button zButton variant="secondary" [loading]="saving()">Speichern</button>`
 */
@Component({
  selector: 'button[zButton], a[zButton]',
  imports: [Spinner],
  host: {
    '[class]': 'classes()',
    '[attr.disabled]': 'loading() || disabled() ? true : null',
    '[attr.aria-busy]': 'loading() || null',
  },
  template: `
    <span class="contents" [class.invisible]="loading()"><ng-content /></span>
    @if (loading()) {
      <z-spinner class="absolute inset-0 m-auto size-5" />
    }
  `,
})
export class Button {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<keyof typeof SIZES>('md');
  readonly loading = input(false);
  readonly disabled = input(false);

  protected readonly classes = computed(
    () => `${BASE} ${VARIANTS[this.variant()]} ${SIZES[this.size()]}`,
  );
}
