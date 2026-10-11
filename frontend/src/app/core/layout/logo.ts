import { Component } from '@angular/core';

@Component({
  selector: 'z-logo',
  host: { class: 'block shrink-0', 'aria-hidden': 'true' },
  template: `<svg viewBox="0 0 64 64" class="size-full">
    <rect width="64" height="64" rx="16" class="fill-accent" />
    <path
      d="M21 21h22L21 43h22"
      fill="none"
      class="stroke-accent-fg"
      stroke-width="6"
      stroke-linecap="round"
      stroke-linejoin="round"
    />
  </svg>`,
})
export class Logo {}
