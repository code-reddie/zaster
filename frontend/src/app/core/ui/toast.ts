import { Component, inject, Injectable, signal } from '@angular/core';
import { Icon } from './icon';

interface Toast {
  id: number;
  text: string;
  kind: 'success' | 'error';
}

/** Kurze Rückmeldung unten am Bildschirm, z. B. „12 Buchungen übernommen“. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<Toast[]>([]);
  private nextId = 1;

  success(text: string) {
    this.show(text, 'success');
  }

  error(text: string) {
    this.show(text, 'error');
  }

  dismiss(id: number) {
    this.toasts.update((t) => t.filter((toast) => toast.id !== id));
  }

  private show(text: string, kind: Toast['kind']) {
    const id = this.nextId++;
    this.toasts.update((t) => [...t.slice(-2), { id, text, kind }]);
    setTimeout(() => this.dismiss(id), kind === 'error' ? 6000 : 3500);
  }
}

@Component({
  selector: 'z-toast-outlet',
  imports: [Icon],
  host: {
    class:
      'pointer-events-none fixed inset-x-0 bottom-0 z-[60] flex flex-col items-center gap-2 px-4 ' +
      'pb-[calc(env(safe-area-inset-bottom)+5rem)] lg:pb-6',
    'aria-live': 'polite',
  },
  template: `
    @for (toast of service.toasts(); track toast.id) {
      <div
        class="pointer-events-auto flex max-w-md items-center gap-2.5 rounded-xl bg-fg px-4 py-3 text-sm text-bg shadow-lg"
        role="status"
      >
        <z-icon
          [name]="toast.kind === 'success' ? 'circle-check' : 'alert'"
          class="size-5"
          [class.text-danger]="toast.kind === 'error'"
        />
        <span>{{ toast.text }}</span>
        <button
          type="button"
          class="-mr-1 ml-1 rounded-md p-1 opacity-60 hover:opacity-100"
          aria-label="Schließen"
          (click)="service.dismiss(toast.id)"
        >
          <z-icon name="x" class="size-4" />
        </button>
      </div>
    }
  `,
})
export class ToastOutlet {
  protected readonly service = inject(ToastService);
}
