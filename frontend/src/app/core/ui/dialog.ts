import { Dialog, DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Overlay } from '@angular/cdk/overlay';
import { ComponentType } from '@angular/cdk/portal';
import { Component, inject, Injectable, input } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Button } from './button';
import { Icon } from './icon';

/**
 * Öffnet Dialoge: auf dem Handy als Sheet von unten (mit dem Daumen erreichbar),
 * ab 640px Breite mittig.
 */
@Injectable({ providedIn: 'root' })
export class DialogService {
  private readonly dialog = inject(Dialog);
  private readonly overlay = inject(Overlay);

  constructor() {
    trackKeyboard();
  }

  open<R, D = unknown, C = unknown>(component: ComponentType<C>, data?: D): DialogRef<R, C> {
    const small = window.matchMedia('(max-width: 639px)').matches;
    const position = this.overlay.position().global().centerHorizontally();
    return this.dialog.open<R, D, C>(component, {
      data,
      autoFocus: small ? 'dialog' : 'first-tabbable',
      restoreFocus: true,
      backdropClass: 'z-backdrop',
      panelClass: ['z-dialog-panel', small ? 'z-sheet' : 'z-modal'],
      positionStrategy: small ? position.bottom('0') : position.centerVertically(),
      width: small ? '100%' : '32rem',
      maxWidth: small ? '100%' : 'calc(100vw - 2rem)',
      maxHeight: small ? '92dvh' : '85dvh',
    });
  }

  /** Öffnet den Dialog und wartet auf sein Ergebnis (undefined = abgebrochen). */
  result<R, D = unknown, C = unknown>(
    component: ComponentType<C>,
    data?: D,
  ): Promise<R | undefined> {
    return firstValueFrom(this.open<R, D, C>(component, data).closed);
  }

  confirm(options: ConfirmOptions): Promise<boolean | undefined> {
    return this.result<boolean, ConfirmOptions, ConfirmDialog>(ConfirmDialog, options);
  }
}

/**
 * Rahmen eines Dialogs: Titel mit Schließen-Taste, scrollbarer Inhalt und Fußzeile für Tasten
 * (`<z-dialog-footer>`).
 */
@Component({
  selector: 'z-dialog',
  imports: [Icon],
  host: {
    class:
      'flex max-h-[inherit] flex-col overflow-hidden bg-surface shadow-2xl ' +
      'rounded-t-3xl sm:rounded-2xl sm:border sm:border-border',
  },
  template: `
    <div class="mx-auto mt-2 h-1.5 w-10 shrink-0 rounded-full bg-border sm:hidden"></div>
    <header class="flex shrink-0 items-start gap-3 px-5 pt-4 pb-2 sm:pt-5">
      <div class="min-w-0 flex-1">
        <h2 class="text-lg font-semibold">{{ title() }}</h2>
        @if (subtitle()) {
          <p class="mt-0.5 truncate text-sm text-muted">{{ subtitle() }}</p>
        }
      </div>
      <button
        type="button"
        class="-mr-2 grid size-9 place-items-center rounded-full text-muted hover:bg-surface-2 hover:text-fg"
        aria-label="Schließen"
        (click)="ref.close()"
      >
        <z-icon name="x" class="size-5" />
      </button>
    </header>
    <div class="min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 pt-2 pb-5">
      <ng-content />
    </div>
    <ng-content select="z-dialog-footer" />
  `,
})
export class DialogFrame {
  protected readonly ref = inject(DialogRef);
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>();
}

/** Fußzeile mit Tasten; auf dem Handy über die ganze Breite und über der Home-Leiste. */
@Component({
  selector: 'z-dialog-footer',
  host: {
    class:
      'flex shrink-0 flex-col-reverse gap-2 border-t border-border px-5 pt-3 ' +
      'pb-[calc(env(safe-area-inset-bottom)+0.75rem)] sm:flex-row sm:justify-end sm:pb-3 ' +
      '[&>*]:w-full sm:[&>*]:w-auto',
  },
  template: `<ng-content />`,
})
export class DialogFooter {}

/**
 * Safari am iPhone verkleinert beim Öffnen der Tastatur nicht die Seite, sondern legt die
 * Tastatur darüber. Ein Sheet, das unten am Bildschirm klebt, liegt dann dahinter; solange es
 * hoch genug ist, schiebt Safari das Eingabefeld noch ins Bild, schrumpft der Inhalt (z. B. bei
 * einer Suche ohne Treffer), rutscht alles unter die Tastatur. Deshalb hier die Höhe der Tastatur
 * und des sichtbaren Bereichs als CSS-Variablen setzen; styles.css hebt die Sheets damit an.
 */
function trackKeyboard() {
  const viewport = window.visualViewport;
  const root = document.documentElement;
  if (!viewport || root.dataset['keyboardTracked']) {
    return;
  }
  root.dataset['keyboardTracked'] = '';
  const update = () => {
    const keyboard = Math.max(0, window.innerHeight - viewport.height - viewport.offsetTop);
    root.style.setProperty('--z-keyboard', `${Math.round(keyboard)}px`);
    root.style.setProperty('--z-viewport-height', `${Math.round(viewport.height)}px`);
  };
  viewport.addEventListener('resize', update);
  viewport.addEventListener('scroll', update);
  update();
}

export interface ConfirmOptions {
  title: string;
  text: string;
  confirm: string;
  danger?: boolean;
}

@Component({
  imports: [DialogFrame, DialogFooter, Button],
  template: `
    <z-dialog [title]="data.title">
      <p class="text-sm text-muted">{{ data.text }}</p>
      <z-dialog-footer>
        <button zButton variant="secondary" type="button" (click)="ref.close(false)">
          Abbrechen
        </button>
        <button
          zButton
          [variant]="data.danger ? 'danger' : 'primary'"
          type="button"
          (click)="ref.close(true)"
        >
          {{ data.confirm }}
        </button>
      </z-dialog-footer>
    </z-dialog>
  `,
})
export class ConfirmDialog {
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
  protected readonly data = inject<ConfirmOptions>(DIALOG_DATA);
}
