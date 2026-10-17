import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PageHeader } from '../../core/layout/page-header';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogService } from '../../core/ui/dialog';
import { EmptyState } from '../../core/ui/empty-state';
import { Icon } from '../../core/ui/icon';
import { Spinner } from '../../core/ui/spinner';
import { AmountPipe, dayLabel, isoDate, monthLabel } from '../../shared/format';
import { AccountStore } from '../accounts/account.store';
import { CategoryStore } from '../categories/category.store';
import { FinTsSyncSheet } from '../fints/fints-sync-sheet';
import { CsvImportSheet } from '../import/csv-import-sheet';
import { CreateTransactionSheet } from './create-transaction-sheet';
import { Transaction, TransactionQuery } from './transaction.models';
import { TransactionRow } from './transaction-row';
import { TransactionSheet, TransactionSheetResult } from './transaction-sheet';
import { TransactionsService } from './transactions.service';
import { UndoService } from './undo.service';

const PAGE_SIZE = 100;

interface Month {
  year: number;
  month: number;
}

interface DayGroup {
  date: string;
  label: string;
  sum: number;
  items: Transaction[];
}

@Component({
  imports: [FormsModule, PageHeader, Button, Icon, Spinner, EmptyState, AmountPipe, TransactionRow],
  templateUrl: './transactions.page.html',
})
export class TransactionsPage {
  protected readonly accounts = inject(AccountStore);
  protected readonly categories = inject(CategoryStore);
  private readonly service = inject(TransactionsService);
  private readonly dialog = inject(DialogService);
  private readonly undo = inject(UndoService);

  /** null = alle Zeiträume (Standard beim Öffnen der Seite) */
  protected readonly month = signal<Month | null>(null);
  protected readonly searchInput = signal('');
  private readonly search = debounced(this.searchInput, 300);
  /** Kategorie-Id, 'none' = ohne Kategorie, '' = alle */
  protected readonly categoryFilter = signal<string>('');

  protected readonly items = signal<Transaction[]>([]);
  protected readonly total = signal(0);
  protected readonly einnahmen = signal(0);
  protected readonly ausgaben = signal(0);
  protected readonly loading = signal(true);
  protected readonly loadingMore = signal(false);
  protected readonly error = signal<string | null>(null);
  private requestId = 0;

  protected readonly monthLabel = computed(() => {
    const m = this.month();
    return m ? monthLabel(m.year, m.month) : 'Alle Zeiträume';
  });

  protected readonly filtered = computed(
    () => !!this.search().trim() || this.categoryFilter() !== '',
  );

  protected readonly groups = computed<DayGroup[]>(() => {
    const groups: DayGroup[] = [];
    for (const t of this.items()) {
      let group = groups.at(-1);
      if (group?.date !== t.buchung) {
        group = { date: t.buchung, label: dayLabel(t.buchung), sum: 0, items: [] };
        groups.push(group);
      }
      group.items.push(t);
      group.sum += t.betrag;
    }
    return groups;
  });

  private readonly query = computed<TransactionQuery>(() => {
    const m = this.month();
    const category = this.categoryFilter();
    return {
      from: m ? isoDate(new Date(m.year, m.month, 1)) : undefined,
      to: m ? isoDate(new Date(m.year, m.month + 1, 0)) : undefined,
      search: this.search().trim() || undefined,
      categoryId: category && category !== 'none' ? Number(category) : undefined,
      withoutCategory: category === 'none',
    };
  });

  constructor() {
    effect(() => {
      const accountId = this.accounts.selectedId();
      const query = this.query();
      if (accountId !== null) {
        untracked(() => this.load(accountId, query));
      }
    });
    // Nach „Rückgängig“ die betroffenen Buchungen in der Liste ersetzen.
    const seen = this.undo.restored();
    effect(() => {
      const restored = this.undo.restored();
      if (restored && restored !== seen) {
        const byId = new Map(restored.items.map((t) => [t.id, t]));
        untracked(() => this.items.update((items) => items.map((t) => byId.get(t.id) ?? t)));
      }
    });
  }

  protected indent(depth: number) {
    return '\u00a0'.repeat(depth * 3);
  }

  protected shiftMonth(delta: number) {
    const m = this.month() ?? currentMonth();
    const date = new Date(m.year, m.month + delta, 1);
    this.month.set({ year: date.getFullYear(), month: date.getMonth() });
  }

  protected toggleAll() {
    this.month.set(this.month() ? null : currentMonth());
  }

  protected resetFilters() {
    this.searchInput.set('');
    this.categoryFilter.set('');
  }

  protected async loadMore() {
    const accountId = this.accounts.selectedId();
    if (accountId === null) {
      return;
    }
    this.loadingMore.set(true);
    try {
      const page = await this.service.list(accountId, {
        ...this.query(),
        offset: this.items().length,
        limit: PAGE_SIZE,
      });
      this.items.update((items) => [...items, ...page.items]);
    } catch (e) {
      this.error.set(errorMessage(e, 'Weitere Buchungen konnten nicht geladen werden.'));
    } finally {
      this.loadingMore.set(false);
    }
  }

  protected async open(transaction: Transaction) {
    const result = await this.dialog.result<TransactionSheetResult>(TransactionSheet, transaction);
    if (!result) {
      return;
    }
    if ('updated' in result) {
      this.items.update((items) =>
        items.map((t) => (t.id === result.updated.id ? result.updated : t)),
      );
    } else {
      this.reload();
    }
    this.refreshAccount();
  }

  protected async sync() {
    if (await this.dialog.result<boolean>(FinTsSyncSheet)) {
      this.reload();
    }
  }

  protected async importCsv() {
    if (await this.dialog.result<boolean>(CsvImportSheet)) {
      this.reload();
    }
  }

  protected async create() {
    if (await this.dialog.result<boolean>(CreateTransactionSheet)) {
      this.reload();
    }
  }

  private reload() {
    const accountId = this.accounts.selectedId();
    if (accountId !== null) {
      this.load(accountId, this.query());
      this.refreshAccount();
    }
  }

  private refreshAccount() {
    const accountId = this.accounts.selectedId();
    if (accountId !== null) {
      this.accounts.refresh(accountId);
    }
  }

  private async load(accountId: number, query: TransactionQuery) {
    const id = ++this.requestId;
    this.loading.set(true);
    this.error.set(null);
    try {
      const page = await this.service.list(accountId, { ...query, limit: PAGE_SIZE });
      if (id === this.requestId) {
        this.items.set(page.items);
        this.total.set(page.total);
        this.einnahmen.set(page.einnahmen);
        this.ausgaben.set(page.ausgaben);
      }
    } catch (e) {
      if (id === this.requestId) {
        this.error.set(errorMessage(e, 'Die Buchungen konnten nicht geladen werden.'));
      }
    } finally {
      if (id === this.requestId) {
        this.loading.set(false);
      }
    }
  }
}

function currentMonth(): Month {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() };
}

/** Signal, das dem Eingabe-Signal erst nach einer Pause folgt (für die Suche). */
function debounced(source: () => string, ms: number) {
  const value = signal(source());
  let timer: ReturnType<typeof setTimeout> | undefined;
  effect(() => {
    const next = source();
    clearTimeout(timer);
    timer = setTimeout(() => value.set(next), next ? ms : 0);
  });
  return value.asReadonly();
}
