import { Component, input } from '@angular/core';
import { Logo } from '../../core/layout/logo';

@Component({
  selector: 'z-auth-layout',
  imports: [Logo],
  host: {
    class:
      'flex min-h-dvh flex-col items-center justify-center px-4 py-10 ' +
      'bg-[radial-gradient(ellipse_at_top,var(--z-accent-soft),transparent_60%)]',
  },
  template: `
    <div class="w-full max-w-sm">
      <div class="mb-8 flex flex-col items-center text-center">
        <z-logo class="mb-4 size-14" />
        <h1 class="text-2xl font-bold tracking-tight">{{ title() }}</h1>
        <p class="mt-1 text-sm text-muted">{{ subtitle() }}</p>
      </div>
      <div class="z-card p-6"><ng-content /></div>
      <p class="mt-6 text-center text-sm text-muted"><ng-content select="[footer]" /></p>
    </div>
  `,
})
export class AuthLayout {
  readonly title = input.required<string>();
  readonly subtitle = input.required<string>();
}
