import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageHeader } from '../../core/layout/page-header';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogService } from '../../core/ui/dialog';
import { Icon } from '../../core/ui/icon';
import { ToastService } from '../../core/ui/toast';
import { Spinner } from '../../core/ui/spinner';
import { AmountPipe, IbanPipe } from '../../shared/format';
import { FinTsService } from '../fints/fints.service';
import { FinTsSyncSheet } from '../fints/fints-sync-sheet';
import { CsvImportSheet } from '../import/csv-import-sheet';
import { BalanceCheck } from '../transactions/transaction.models';
import { TransactionsService } from '../transactions/transactions.service';
import { AccountStore } from './account.store';

@Component({
  imports: [FormsModule, DatePipe, PageHeader, Button, Icon, Spinner, IbanPipe, AmountPipe],
  templateUrl: './account.page.html',
})
export class AccountPage {
  protected readonly store = inject(AccountStore);
  private readonly finTs = inject(FinTsService);
  private readonly transactions = inject(TransactionsService);
  private readonly dialog = inject(DialogService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  protected readonly name = signal('');
  protected readonly finTsUserId = signal('');
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly newTime = signal('');
  protected readonly savingTimes = signal(false);
  protected readonly balance = signal<BalanceCheck | null>(null);
  protected readonly balanceFailed = signal(false);
  protected readonly shownGaps = computed(() => this.balance()?.luecken.slice(0, 10) ?? []);

  constructor() {
    // Formular mit dem gewählten Konto füllen, auch nach einem Kontowechsel.
    effect(() => {
      const account = this.store.selected();
      this.name.set(account?.name ?? '');
      this.finTsUserId.set(account?.finTsUserId ?? '');
    });
    effect(() => {
      const accountId = this.store.selected()?.id;
      if (accountId !== undefined) {
        this.loadBalance(accountId);
      }
    });
  }

  private async loadBalance(accountId: number) {
    this.balance.set(null);
    this.balanceFailed.set(false);
    try {
      const check = await this.transactions.balanceCheck(accountId);
      if (this.store.selected()?.id === accountId) {
        this.balance.set(check);
      }
    } catch {
      // Die Prüfung ist nur ein Hinweis; ohne sie bleibt der Rest der Seite benutzbar.
      this.balanceFailed.set(true);
    }
  }

  protected dirty() {
    const account = this.store.selected();
    return (
      !!account &&
      (this.name().trim() !== account.name ||
        (this.finTsUserId().trim() || null) !== account.finTsUserId)
    );
  }

  protected async save() {
    const account = this.store.selected();
    if (!account) {
      return;
    }
    if (!this.name().trim()) {
      this.error.set('Bitte gib dem Konto einen Namen.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.store.update(account.id, {
        name: this.name().trim(),
        finTsUserId: this.finTsUserId().trim() || null,
      });
      this.toast.success('Gespeichert.');
    } catch (e) {
      this.error.set(errorMessage(e, 'Das Konto konnte nicht gespeichert werden.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async addTime() {
    const account = this.store.selected();
    const time = this.newTime();
    if (!account || !time) {
      return;
    }
    if (account.syncTimes.includes(time)) {
      this.newTime.set('');
      return;
    }
    if (await this.saveTimes(account.id, [...account.syncTimes, time])) {
      this.newTime.set('');
    }
  }

  protected async removeTime(time: string) {
    const account = this.store.selected();
    if (account) {
      await this.saveTimes(
        account.id,
        account.syncTimes.filter((t) => t !== time),
      );
    }
  }

  private async saveTimes(accountId: number, times: string[]) {
    this.savingTimes.set(true);
    try {
      await this.finTs.updateSyncTimes(accountId, times);
      await this.store.refresh(accountId);
      return true;
    } catch (e) {
      this.toast.error(errorMessage(e, 'Die Uhrzeiten konnten nicht gespeichert werden.'));
      return false;
    } finally {
      this.savingTimes.set(false);
    }
  }

  protected async sync() {
    await this.dialog.result(FinTsSyncSheet);
    this.reloadBalance();
  }

  protected async importCsv() {
    await this.dialog.result(CsvImportSheet);
    this.reloadBalance();
  }

  private reloadBalance() {
    const account = this.store.selected();
    if (account) {
      this.loadBalance(account.id);
    }
  }

  protected async deletePin() {
    const account = this.store.selected();
    if (!account) {
      return;
    }
    const confirmed = await this.dialog.confirm({
      title: 'PIN löschen?',
      text: 'Zaster ruft dieses Konto dann nicht mehr automatisch ab. Beim nächsten Abruf fragt es wieder nach der PIN.',
      confirm: 'PIN löschen',
      danger: true,
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.finTs.deletePin(account.id);
      await this.store.refresh(account.id);
      this.toast.success('PIN gelöscht.');
    } catch (e) {
      this.toast.error(errorMessage(e, 'Die PIN konnte nicht gelöscht werden.'));
    }
  }

  protected async delete() {
    const account = this.store.selected();
    if (!account) {
      return;
    }
    const others = account.members.length - 1;
    const confirmed = await this.dialog.confirm({
      title: `„${account.name}“ löschen?`,
      text:
        'Alle Buchungen, Kategorien und Regeln dieses Kontos werden endgültig gelöscht' +
        (others > 0
          ? `, auch für ${others === 1 ? 'das andere Mitglied' : `die ${others} anderen Mitglieder`}.`
          : '.'),
      confirm: 'Endgültig löschen',
      danger: true,
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.store.delete(account.id);
      this.toast.success('Konto gelöscht.');
      this.router.navigateByUrl('/');
    } catch (e) {
      this.toast.error(errorMessage(e, 'Das Konto konnte nicht gelöscht werden.'));
    }
  }
}
