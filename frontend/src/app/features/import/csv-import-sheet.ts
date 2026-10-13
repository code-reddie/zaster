import { DialogRef } from '@angular/cdk/dialog';
import { Component, computed, inject, signal } from '@angular/core';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogFooter, DialogFrame } from '../../core/ui/dialog';
import { Icon } from '../../core/ui/icon';
import { AmountPipe } from '../../shared/format';
import { AccountStore } from '../accounts/account.store';
import { ImportResult, ImportTransaction } from '../transactions/transaction.models';
import { TransactionsService } from '../transactions/transactions.service';
import { decodeCsv, parseCsv } from './csv-parser';
import { ImportFileError } from './trade-republic-parser';

/**
 * CSV-Export der ING, Transaktionsexport von Trade Republic oder eine eigene Umsatzliste ins gewählte
 * Konto übernehmen (Aufbau siehe `parseCsv`).
 * Ergebnis true, wenn übernommen.
 */
@Component({
  imports: [DialogFrame, DialogFooter, Button, Icon, AmountPipe],
  template: `
    <z-dialog title="CSV importieren" [subtitle]="accounts.selected()?.name">
      @if (result(); as r) {
        <div class="flex flex-col items-center py-4 text-center">
          <div class="mb-3 grid size-14 place-items-center rounded-2xl bg-accent-soft text-accent">
            <z-icon name="circle-check" class="size-7" />
          </div>
          <p class="text-lg font-semibold">
            {{ r.imported }} {{ r.imported === 1 ? 'Buchung' : 'Buchungen' }} übernommen
          </p>
          <p class="text-sm text-muted">
            {{ r.skipped === 1 ? 'Eine war' : r.skipped + ' waren' }} schon vorhanden.
          </p>
        </div>
      } @else {
        <label
          class="flex cursor-pointer flex-col items-center rounded-2xl border-2 border-dashed px-4 py-8 text-center transition-colors"
          [class]="dragging() ? 'border-accent bg-accent-soft' : 'border-border hover:bg-surface-2'"
          (dragover)="$event.preventDefault(); dragging.set(true)"
          (dragleave)="dragging.set(false)"
          (drop)="drop($event)"
        >
          <z-icon name="file" class="mb-2 size-8 text-muted" />
          @if (fileName()) {
            <span class="font-medium">{{ fileName() }}</span>
            <span class="mt-1 text-sm text-muted">Andere Datei wählen</span>
          } @else {
            <span class="font-medium">CSV-Datei wählen</span>
            <span class="mt-1 text-sm text-muted"
              >Umsatzexport der ING, Transaktionsexport von Trade Republic oder eigene Umsatzliste
              (.csv)</span
            >
          }
          <input type="file" accept=".csv,text/csv" class="sr-only" (change)="select($event)" />
        </label>

        @if (transactions().length) {
          <dl class="mt-4 grid grid-cols-2 gap-2 text-sm">
            <div class="rounded-xl bg-surface-2 px-3 py-2.5">
              <dt class="text-muted">Buchungen</dt>
              <dd class="font-semibold">{{ transactions().length }}</dd>
            </div>
            <div class="rounded-xl bg-surface-2 px-3 py-2.5">
              <dt class="text-muted">Zeitraum</dt>
              <dd class="font-semibold">{{ range() }}</dd>
            </div>
            <div class="rounded-xl bg-surface-2 px-3 py-2.5">
              <dt class="text-muted">Einnahmen</dt>
              <dd class="amount font-semibold text-positive">{{ sum(true) | amount }}</dd>
            </div>
            <div class="rounded-xl bg-surface-2 px-3 py-2.5">
              <dt class="text-muted">Ausgaben</dt>
              <dd class="amount font-semibold">{{ -sum(false) | amount }}</dd>
            </div>
          </dl>
          <p class="z-hint">
            Buchungen, die schon im Konto sind (auch per FinTS abgerufene), überspringt Zaster.
          </p>
        }
        @if (error()) {
          <p class="z-error mt-4">{{ error() }}</p>
        }
      }

      <z-dialog-footer>
        @if (result()) {
          <button zButton type="button" (click)="ref.close(true)">Fertig</button>
        } @else {
          <button zButton variant="secondary" type="button" (click)="ref.close()">Abbrechen</button>
          <button
            zButton
            type="button"
            [disabled]="!transactions().length"
            [loading]="importing()"
            (click)="import()"
          >
            Übernehmen
          </button>
        }
      </z-dialog-footer>
    </z-dialog>
  `,
})
export class CsvImportSheet {
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
  protected readonly accounts = inject(AccountStore);
  private readonly service = inject(TransactionsService);

  protected readonly fileName = signal<string | null>(null);
  protected readonly transactions = signal<ImportTransaction[]>([]);
  protected readonly dragging = signal(false);
  protected readonly importing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<ImportResult | null>(null);

  protected readonly range = computed(() => {
    const dates = this.transactions()
      .map((t) => t.buchung)
      .sort();
    const format = (d: string) => d.split('-').reverse().join('.');
    return dates.length ? `${format(dates[0])} – ${format(dates.at(-1)!)}` : '';
  });

  protected sum(positive: boolean) {
    return this.transactions()
      .filter((t) => (positive ? t.betrag > 0 : t.betrag < 0))
      .reduce((s, t) => s + t.betrag, 0);
  }

  protected select(event: Event) {
    this.read((event.target as HTMLInputElement).files?.[0]);
  }

  protected drop(event: DragEvent) {
    event.preventDefault();
    this.dragging.set(false);
    this.read(event.dataTransfer?.files[0]);
  }

  protected async import() {
    const accountId = this.accounts.selectedId();
    if (accountId === null) {
      return;
    }
    this.importing.set(true);
    this.error.set(null);
    try {
      this.result.set(await this.service.import(accountId, this.transactions()));
      await this.accounts.refresh(accountId);
    } catch (e) {
      this.error.set(errorMessage(e, 'Der Import ist fehlgeschlagen.'));
    } finally {
      this.importing.set(false);
    }
  }

  private async read(file: File | undefined) {
    if (!file) {
      return;
    }
    this.fileName.set(file.name);
    this.error.set(null);
    let transactions: ImportTransaction[];
    try {
      transactions = parseCsv(decodeCsv(await file.arrayBuffer()));
    } catch (e) {
      this.transactions.set([]);
      this.error.set(e instanceof ImportFileError ? e.message : 'Die Datei ließ sich nicht lesen.');
      return;
    }
    this.transactions.set(transactions);
    if (transactions.length === 0) {
      this.error.set(
        'In der Datei wurden keine Buchungen gefunden. Erwartet wird eine CSV mit Semikolon und der Kopfzeile „Buchung;…“, etwa der Umsatzexport der ING.',
      );
    }
  }
}
