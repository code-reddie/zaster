import { inject, Injectable, signal } from '@angular/core';
import { errorMessage } from '../../core/errors';
import { ToastService } from '../../core/ui/toast';
import { AccountStore } from '../accounts/account.store';
import { Transaction, TransactionState } from './transaction.models';
import { TransactionsService } from './transactions.service';

/** Buchungen, die „Rückgängig“ gerade zurückgesetzt hat. */
export interface Restored {
  accountId: number;
  items: Transaction[];
}

/**
 * „Rückgängig“ für Aktionen an Buchungen: Kategorie zuordnen, aufteilen, nicht kategorisieren,
 * Bemerkung, alle nicht kategorisieren und Regeln anwenden. Nach der Aktion zeigt eine Meldung
 * die Taste; sie setzt die Buchungen auf den Zustand von vorher zurück. Es gibt sie nur für die
 * letzte Aktion, eine neue ersetzt die alte Meldung.
 */
@Injectable({ providedIn: 'root' })
export class UndoService {
  private readonly service = inject(TransactionsService);
  private readonly toast = inject(ToastService);
  private readonly accounts = inject(AccountStore);

  /** Seiten und Dialoge übernehmen daraus die zurückgesetzten Buchungen. */
  readonly restored = signal<Restored | null>(null);

  /**
   * Meldung mit „Rückgängig“. `previous` ist der Zustand der geänderten Buchungen vor der
   * Aktion; ist er leer, gibt es nichts zurückzunehmen und die Meldung bleibt ohne Taste.
   */
  offer(
    text: string,
    accountId: number,
    previous: TransactionState[],
    done = 'Rückgängig gemacht.',
  ) {
    if (previous.length === 0) {
      this.toast.success(text);
      return;
    }
    this.toast.success(text, {
      label: 'Rückgängig',
      run: () => this.undo(accountId, previous, done),
    });
  }

  private async undo(accountId: number, previous: TransactionState[], done: string) {
    try {
      const items = await this.service.restore(accountId, previous);
      this.restored.set({ accountId, items });
      this.accounts.refresh(accountId);
      this.toast.success(done);
    } catch (e) {
      this.toast.error(errorMessage(e, 'Das ließ sich nicht rückgängig machen.'));
    }
  }
}
