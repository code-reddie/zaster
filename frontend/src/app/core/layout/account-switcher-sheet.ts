import { DialogRef } from '@angular/cdk/dialog';
import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AccountStore } from '../../features/accounts/account.store';
import { IbanPipe } from '../../shared/format';
import { DialogFrame } from '../ui/dialog';
import { Icon } from '../ui/icon';

@Component({
  imports: [DialogFrame, Icon, IbanPipe],
  template: `
    <z-dialog title="Konto wählen">
      <div class="-mx-2 flex flex-col">
        @for (account of store.accounts(); track account.id) {
          <button
            type="button"
            class="flex items-center gap-3 rounded-xl px-2 py-3 text-left hover:bg-surface-2"
            [class.bg-surface-2]="account.id === store.selectedId()"
            (click)="go('/konten/' + account.id)"
          >
            <span class="grid size-10 place-items-center rounded-xl bg-accent-soft text-accent">
              <z-icon [name]="account.members.length > 1 ? 'users' : 'wallet'" class="size-5" />
            </span>
            <span class="min-w-0 flex-1">
              <span class="block truncate font-medium">{{ account.name }}</span>
              <span class="block truncate text-xs text-muted">{{ account.iban | iban }}</span>
            </span>
            @if (account.id === store.selectedId()) {
              <z-icon name="check" class="size-5 text-accent" />
            }
          </button>
        }
        <button
          type="button"
          class="flex items-center gap-3 rounded-xl px-2 py-3 text-left text-muted hover:bg-surface-2"
          (click)="go('/konten/neu')"
        >
          <span
            class="grid size-10 place-items-center rounded-xl border border-dashed border-border"
          >
            <z-icon name="plus" class="size-5" />
          </span>
          Konto hinzufügen
        </button>
      </div>
    </z-dialog>
  `,
})
export class AccountSwitcherSheet {
  protected readonly store = inject(AccountStore);
  private readonly ref = inject(DialogRef);
  private readonly router = inject(Router);

  protected go(url: string) {
    this.ref.close();
    this.router.navigateByUrl(url);
  }
}
