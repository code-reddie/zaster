import { Component, input } from '@angular/core';
import { Icon, IconName } from './icon';

@Component({
  selector: 'z-empty-state',
  imports: [Icon],
  host: { class: 'flex flex-col items-center px-6 py-14 text-center' },
  template: `
    <div class="mb-4 grid size-14 place-items-center rounded-2xl bg-accent-soft text-accent">
      <z-icon [name]="icon()" class="size-7" />
    </div>
    <h2 class="text-base font-semibold">{{ title() }}</h2>
    @if (text()) {
      <p class="mt-1 max-w-sm text-sm text-muted">{{ text() }}</p>
    }
    <div class="mt-5 flex flex-wrap justify-center gap-2"><ng-content /></div>
  `,
})
export class EmptyState {
  readonly icon = input<IconName>('inbox');
  readonly title = input.required<string>();
  readonly text = input<string>();
}
