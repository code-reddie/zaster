import { Component } from '@angular/core';

@Component({
  selector: 'z-spinner',
  host: { class: 'block size-5', role: 'status', 'aria-label': 'Lädt' },
  template: `<svg class="size-full animate-spin" viewBox="0 0 24 24" fill="none">
    <circle cx="12" cy="12" r="9" stroke="currentColor" stroke-width="3" class="opacity-20" />
    <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" stroke-width="3" stroke-linecap="round" />
  </svg>`,
})
export class Spinner {}
