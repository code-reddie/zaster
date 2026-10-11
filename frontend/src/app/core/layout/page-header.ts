import { booleanAttribute, Component, input } from '@angular/core';

/** Seitentitel mit optionalem Untertitel; Tasten kommen als Inhalt rechts daneben. */
@Component({
  selector: 'z-page-header',
  host: { class: 'mb-5 flex flex-wrap items-end gap-x-4 gap-y-3 lg:mb-7' },
  template: `
    <div class="min-w-0 flex-1">
      <h1 class="text-2xl font-bold tracking-tight lg:text-[28px]">{{ title() }}</h1>
      @if (subtitle()) {
        <p class="mt-1 text-sm text-muted" [class.max-lg:hidden]="subtitleDesktopOnly()">
          {{ subtitle() }}
        </p>
      }
    </div>
    <div class="flex items-center gap-2"><ng-content /></div>
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>();
  /** Kontoname als Untertitel: auf dem Handy steht er schon in der Kopfzeile. */
  readonly subtitleDesktopOnly = input(false, { transform: booleanAttribute });
}
