import { DialogRef } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogFooter, DialogFrame } from '../../core/ui/dialog';
import { Icon } from '../../core/ui/icon';
import { AccountStore } from '../accounts/account.store';
import { FinTsService, FinTsSyncResult } from './fints.service';

/**
 * Umsätze per FinTS abrufen. Mit gespeicherter PIN reicht ein Tipp.
 * Ergebnis true, wenn der Abruf geklappt hat (Liste neu laden).
 */
@Component({
  imports: [FormsModule, DatePipe, DialogFrame, DialogFooter, Button, Icon],
  template: `
    <z-dialog title="Umsätze abrufen" [subtitle]="account()?.name">
      @if (result(); as r) {
        @if (r.success) {
          <div class="flex flex-col items-center py-4 text-center">
            <div
              class="mb-3 grid size-14 place-items-center rounded-2xl bg-accent-soft text-accent"
            >
              <z-icon name="circle-check" class="size-7" />
            </div>
            <p class="text-lg font-semibold">
              {{ r.imported }} {{ r.imported === 1 ? 'neue Buchung' : 'neue Buchungen' }}
            </p>
            <p class="text-sm text-muted">
              {{ r.skipped === 1 ? 'Eine war' : r.skipped + ' waren' }} schon vorhanden.
            </p>
          </div>
        } @else {
          <p class="z-error">{{ r.error }}</p>
        }
        @if (r.messages.length) {
          <h3 class="mt-4 mb-1.5 text-sm font-medium">Meldungen der Bank</h3>
          <ul class="flex flex-col gap-1 text-sm text-muted">
            @for (m of r.messages; track $index) {
              <li>
                <span class="font-mono text-xs">{{ m.code }}</span> {{ m.message }}
              </li>
            }
          </ul>
        }
        @if (!r.success && r.diagnostics.length) {
          <details class="mt-4 text-sm">
            <summary class="cursor-pointer text-muted">Technische Details</summary>
            <ul class="mt-2 flex flex-col gap-1 font-mono text-xs break-all text-muted">
              @for (d of r.diagnostics; track $index) {
                <li>{{ d }}</li>
              }
            </ul>
          </details>
        }
      } @else {
        <form id="fints-form" class="flex flex-col gap-4" (ngSubmit)="sync()">
          @if (account(); as a) {
            @if (a.hasFinTsPin) {
              <p class="flex items-start gap-2.5 rounded-xl bg-accent-soft px-3 py-2.5 text-sm">
                <z-icon name="key" class="mt-0.5 size-4 text-accent" />
                <span>
                  Die PIN ist gespeichert, Zaster ruft automatisch ab.
                  @if (a.lastSyncedAt) {
                    Zuletzt am {{ a.lastSyncedAt | date: 'd. MMMM, HH:mm' }} Uhr.
                  }
                </span>
              </p>
            }
            @if (a.lastSyncError) {
              <p class="z-error">Letzter Abruf: {{ a.lastSyncError }}</p>
            }
            @if (!a.hasFinTsPin || showFields()) {
              <div>
                <label class="z-label" for="pin">PIN fürs Online-Banking</label>
                <input
                  id="pin"
                  name="pin"
                  type="password"
                  class="z-input"
                  autocomplete="off"
                  [(ngModel)]="pin"
                />
              </div>
              <div>
                <label class="z-label" for="userId">Zugangsnummer (optional)</label>
                <input
                  id="userId"
                  name="userId"
                  class="z-input"
                  autocomplete="off"
                  [placeholder]="'Leer = aus der IBAN'"
                  [(ngModel)]="userId"
                />
                <p class="z-hint">
                  Bei der ING die 10-stellige Zugangsnummer, meist die letzten Stellen der IBAN.
                </p>
              </div>
              <label class="flex items-start gap-3">
                <input
                  type="checkbox"
                  name="savePin"
                  class="z-checkbox mt-0.5"
                  [(ngModel)]="savePin"
                />
                <span class="text-sm">
                  PIN verschlüsselt speichern
                  <span class="block text-xs text-muted"
                    >Dann ruft Zaster zu den eingestellten Uhrzeiten automatisch ab, und ein Tipp
                    reicht.</span
                  >
                </span>
              </label>
            } @else {
              <button
                type="button"
                class="self-start text-sm text-accent hover:underline"
                (click)="showFields.set(true)"
              >
                Andere PIN oder Zugangsnummer verwenden
              </button>
            }
          }
          @if (error()) {
            <p class="z-error">{{ error() }}</p>
          }
          <p class="text-xs text-muted">
            Die Bank liefert höchstens 90 Tage. Fragt die ING nach einer Freigabe, bestätige sie in
            der Banking-App.
          </p>
        </form>
      }

      <z-dialog-footer>
        @if (result()) {
          <button zButton type="button" (click)="ref.close(result()!.success)">Fertig</button>
        } @else {
          <button zButton variant="secondary" type="button" (click)="ref.close()">Abbrechen</button>
          <button zButton type="submit" form="fints-form" [loading]="loading()">
            <z-icon name="refresh" class="size-4" /> Abrufen
          </button>
        }
      </z-dialog-footer>
    </z-dialog>
  `,
})
export class FinTsSyncSheet {
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
  private readonly accounts = inject(AccountStore);
  private readonly service = inject(FinTsService);

  protected readonly account = this.accounts.selected;
  protected readonly pin = signal('');
  protected readonly userId = signal(this.account()?.finTsUserId ?? '');
  protected readonly savePin = signal(true);
  protected readonly showFields = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<FinTsSyncResult | null>(null);

  protected async sync() {
    const account = this.account();
    if (!account) {
      return;
    }
    if (!account.hasFinTsPin && !this.pin()) {
      this.error.set('Bitte gib deine PIN ein.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await this.service.sync(account.id, {
        userId: this.userId().trim() || null,
        pin: this.pin() || null,
        savePin: this.savePin(),
      });
      this.result.set(result);
    } catch (e) {
      this.error.set(errorMessage(e, 'Der Abruf ist fehlgeschlagen.'));
    } finally {
      this.pin.set('');
      this.loading.set(false);
      await this.accounts.refresh(account.id);
    }
  }
}
