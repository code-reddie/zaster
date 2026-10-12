import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { PageHeader } from '../../core/layout/page-header';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogService } from '../../core/ui/dialog';
import { EmptyState } from '../../core/ui/empty-state';
import { Icon } from '../../core/ui/icon';
import { Spinner } from '../../core/ui/spinner';
import { ToastService } from '../../core/ui/toast';
import { dayLabel } from '../../shared/format';
import { AccountStore } from '../accounts/account.store';
import { CategoryDot } from '../categories/category-dot';
import { CategoryPicker, CategoryPickerData } from '../categories/category-picker';
import { CategoryStore } from '../categories/category.store';
import { RuleSheet, RuleSheetData } from '../categories/rule-sheet';
import { Transaction } from './transaction.models';
import { SplitSheet } from './split-sheet';
import { TransactionRow } from './transaction-row';
import { ruleFromTransaction, TransactionSheet, TransactionSheetResult } from './transaction-sheet';
import { TransactionsService } from './transactions.service';

const PAGE_SIZE = 50;
const RECENT_COUNT = 5;
const RECENT_KEY = 'zaster_recent_categories';

/**
 * „Ohne Kategorie“: alle offenen Buchungen (ohne Kategorie, nicht geprüft) abarbeiten.
 * Ziel des Links in der Push-Nachricht.
 */
@Component({
  imports: [PageHeader, Button, Icon, Spinner, EmptyState, TransactionRow, CategoryDot],
  templateUrl: './open.page.html',
})
export class OpenPage {
  protected readonly accounts = inject(AccountStore);
  protected readonly categories = inject(CategoryStore);
  private readonly service = inject(TransactionsService);
  private readonly dialog = inject(DialogService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  protected readonly items = signal<Transaction[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly busy = signal<number | null>(null);
  protected readonly error = signal<string | null>(null);
  /** Zuletzt gewählte Kategorien als Schnellwahl, pro Nutzer und Konto im Local Storage. */
  private readonly recentIds = signal<number[]>([]);

  protected readonly recent = computed(() =>
    this.recentIds()
      .map((id) => this.categories.byId().get(id))
      .filter((c) => c !== undefined),
  );

  constructor() {
    effect(() => {
      const accountId = this.accounts.selectedId();
      if (accountId !== null) {
        untracked(() => {
          this.recentIds.set(this.readRecent(accountId));
          this.load(accountId);
        });
      }
    });
  }

  protected day(value: string) {
    return dayLabel(value);
  }

  protected async pick(transaction: Transaction) {
    const categoryId = await this.dialog.result<number | null, CategoryPickerData>(CategoryPicker, {
      selected: null,
      subtitle: transaction.auftraggeber,
    });
    if (categoryId !== undefined && categoryId !== null) {
      await this.assign(transaction, categoryId);
    }
  }

  protected async assign(transaction: Transaction, categoryId: number) {
    await this.run(transaction, () =>
      this.service.setCategory(transaction.accountId, transaction.id, categoryId),
    );
    this.recentIds.update((ids) =>
      [
        categoryId,
        ...ids.filter((id) => id !== categoryId && this.categories.byId().has(id)),
      ].slice(0, RECENT_COUNT),
    );
    this.writeRecent(transaction.accountId, this.recentIds());
  }

  protected async split(transaction: Transaction) {
    const updated = await this.dialog.result<Transaction, Transaction>(SplitSheet, transaction);
    if (updated) {
      await this.run(transaction, async () => updated);
    }
  }

  protected async markReviewed(transaction: Transaction) {
    await this.run(transaction, () =>
      this.service.setReviewed(transaction.accountId, transaction.id, true),
    );
  }

  protected async details(transaction: Transaction) {
    const result = await this.dialog.result<TransactionSheetResult>(TransactionSheet, transaction);
    if (result) {
      this.reload();
    }
  }

  /** Regel aus der Buchung anlegen; hat sie gleich Buchungen zugeordnet, Liste neu laden. */
  protected async createRule(transaction: Transaction) {
    const applied = await this.dialog.result<number, RuleSheetData>(
      RuleSheet,
      ruleFromTransaction(transaction),
    );
    if (applied) {
      this.reload();
    }
  }

  protected async reviewAll() {
    const accountId = this.accounts.selectedId();
    const confirmed = await this.dialog.confirm({
      title: 'Alle nicht kategorisieren?',
      text: `${this.total()} Buchungen bleiben ohne Kategorie, verschwinden aber von dieser Seite. In der Buchungsliste lässt sich jede später noch zuordnen.`,
      confirm: 'Alle nicht kategorisieren',
    });
    if (!confirmed || accountId === null) {
      return;
    }
    try {
      const updated = await this.service.review(accountId);
      this.toast.success(`${updated} Buchungen als geprüft markiert.`);
      this.reload();
    } catch (e) {
      this.toast.error(errorMessage(e, 'Das hat nicht geklappt.'));
    }
  }

  protected async applyRules() {
    try {
      const updated = await this.categories.applyRules();
      this.toast.success(
        updated ? `${updated} Buchungen per Regel zugeordnet.` : 'Keine Regel hat gepasst.',
      );
      if (updated) {
        this.reload();
      }
    } catch (e) {
      this.toast.error(errorMessage(e, 'Die Regeln konnten nicht angewendet werden.'));
    }
  }

  protected async loadMore() {
    const accountId = this.accounts.selectedId();
    if (accountId === null) {
      return;
    }
    const page = await this.service.list(accountId, {
      open: true,
      offset: this.items().length,
      limit: PAGE_SIZE,
    });
    this.items.update((items) => [...items, ...page.items]);
  }

  /** Führt die Änderung aus und nimmt die Buchung aus der Liste. */
  private async run(transaction: Transaction, action: () => Promise<Transaction>) {
    this.busy.set(transaction.id);
    try {
      await action();
      this.items.update((items) => items.filter((t) => t.id !== transaction.id));
      this.total.update((n) => n - 1);
      this.accounts.refresh(transaction.accountId);
      if (this.items().length === 0 && this.total() > 0) {
        this.reload();
      }
    } catch (e) {
      this.toast.error(errorMessage(e, 'Die Buchung konnte nicht gespeichert werden.'));
    } finally {
      this.busy.set(null);
    }
  }

  private reload() {
    const accountId = this.accounts.selectedId();
    if (accountId !== null) {
      this.load(accountId);
      this.accounts.refresh(accountId);
    }
  }

  private async load(accountId: number) {
    this.loading.set(true);
    this.error.set(null);
    try {
      const page = await this.service.list(accountId, { open: true, limit: PAGE_SIZE });
      this.items.set(page.items);
      this.total.set(page.total);
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Buchungen konnten nicht geladen werden.'));
    } finally {
      this.loading.set(false);
    }
  }

  private recentKey(accountId: number) {
    return `${RECENT_KEY}:${this.auth.userName() ?? ''}:${accountId}`;
  }

  /** Gelöschte Kategorien fallen in `recent` heraus, weil sie nicht mehr im Store stehen. */
  private readRecent(accountId: number): number[] {
    try {
      const ids: unknown = JSON.parse(localStorage.getItem(this.recentKey(accountId)) ?? '[]');
      return Array.isArray(ids)
        ? ids.filter((id): id is number => Number.isInteger(id)).slice(0, RECENT_COUNT)
        : [];
    } catch {
      return [];
    }
  }

  private writeRecent(accountId: number, ids: number[]) {
    try {
      localStorage.setItem(this.recentKey(accountId), JSON.stringify(ids));
    } catch {
      // Ohne Speicher gilt die Schnellwahl eben nur bis zum Seitenwechsel.
    }
  }
}
