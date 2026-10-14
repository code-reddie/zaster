import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, computed, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { errorMessage } from '../../core/errors';
import { PageHeader } from '../../core/layout/page-header';
import { EmptyState } from '../../core/ui/empty-state';
import { Icon } from '../../core/ui/icon';
import { Spinner } from '../../core/ui/spinner';
import { AmountPipe, isoDate, monthLabel } from '../../shared/format';
import { AccountStore } from '../accounts/account.store';
import { CategoryStore } from '../categories/category.store';
import { buildFlowTree, FlowNode, Flows } from './flow.models';
import { SankeyChart } from './sankey-chart';

type PeriodMode = 'monat' | 'jahr' | 'alles' | 'zeitraum';

interface Row {
  node: FlowNode;
  depth: number;
  expanded: boolean;
}

/**
 * „Verteilung“: woher das Geld im gewählten Zeitraum kam und wohin es ging, als Sankey-Diagramm
 * und darunter als Liste. Kategorien mit Unterkategorien lassen sich in beiden auf- und zuklappen.
 * Ab 640 px (iPad, PC) sind sie zu Beginn alle aufgeklappt, auf dem iPhone alle zugeklappt.
 */
@Component({
  imports: [FormsModule, PageHeader, Icon, Spinner, EmptyState, AmountPipe, SankeyChart],
  templateUrl: './flows.page.html',
})
export class FlowsPage {
  protected readonly accounts = inject(AccountStore);
  private readonly categories = inject(CategoryStore);
  private readonly http = inject(HttpClient);

  protected readonly modes: { value: PeriodMode; label: string }[] = [
    { value: 'monat', label: 'Monat' },
    { value: 'jahr', label: 'Jahr' },
    { value: 'alles', label: 'Alles' },
    { value: 'zeitraum', label: 'Zeitraum' },
  ];
  protected readonly mode = signal<PeriodMode>('jahr');
  /** Bezugspunkt für Monat und Jahr. */
  protected readonly anchor = signal(firstOfMonth(new Date()));
  protected readonly customFrom = signal(isoDate(new Date(new Date().getFullYear(), 0, 1)));
  protected readonly customTo = signal(isoDate(new Date()));

  protected readonly flows = signal<Flows | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  /** Ab 640 px ist Platz für alle Unterkategorien, darunter (iPhone) sind sie anfangs zu. */
  private readonly wide = signal(false);
  /** Kategorien, die von Hand gegenüber dem Anfangszustand auf- oder zugeklappt wurden. */
  private readonly toggled = signal<ReadonlySet<string>>(new Set());
  protected readonly expanded = computed<ReadonlySet<string>>(() => {
    const toggled = this.toggled();
    if (!this.wide()) {
      return toggled;
    }
    const tree = this.tree();
    const keys = tree ? expandableKeys([...tree.income, ...tree.expense]) : [];
    return new Set(keys.filter((key) => !toggled.has(key)));
  });
  private requestId = 0;

  protected readonly range = computed(() => {
    const a = this.anchor();
    switch (this.mode()) {
      case 'monat':
        return {
          from: isoDate(a),
          to: isoDate(new Date(a.getFullYear(), a.getMonth() + 1, 0)),
        };
      case 'jahr':
        return {
          from: isoDate(new Date(a.getFullYear(), 0, 1)),
          to: isoDate(new Date(a.getFullYear(), 11, 31)),
        };
      case 'zeitraum':
        return { from: this.customFrom() || undefined, to: this.customTo() || undefined };
      default:
        return { from: undefined, to: undefined };
    }
  });

  protected readonly periodLabel = computed(() => {
    const a = this.anchor();
    return this.mode() === 'monat'
      ? monthLabel(a.getFullYear(), a.getMonth())
      : String(a.getFullYear());
  });

  protected readonly tree = computed(() => {
    const flows = this.flows();
    return flows ? buildFlowTree(flows, this.categories.tree()) : null;
  });

  protected readonly incomeRows = computed(() => this.rows(this.tree()?.income ?? []));
  protected readonly expenseRows = computed(() => this.rows(this.tree()?.expense ?? []));

  constructor() {
    const media = window.matchMedia('(min-width: 640px)');
    this.wide.set(media.matches);
    const onChange = (e: MediaQueryListEvent) => this.wide.set(e.matches);
    media.addEventListener('change', onChange);
    inject(DestroyRef).onDestroy(() => media.removeEventListener('change', onChange));

    effect(() => {
      const accountId = this.accounts.selectedId();
      const range = this.range();
      if (accountId !== null) {
        untracked(() => this.load(accountId, range));
      }
    });

    // Auf- und Zuklappen von Hand gilt nur für das Konto, in dem es geschah, und nur für die
    // Breite, in der es geschah (wird das Fenster schmaler, gilt wieder der Anfangszustand).
    effect(() => {
      this.accounts.selectedId();
      this.wide();
      untracked(() => this.toggled.set(new Set()));
    });
  }

  protected setMode(mode: PeriodMode) {
    this.mode.set(mode);
  }

  protected shift(delta: number) {
    const a = this.anchor();
    this.anchor.set(
      this.mode() === 'monat'
        ? new Date(a.getFullYear(), a.getMonth() + delta, 1)
        : new Date(a.getFullYear() + delta, a.getMonth(), 1),
    );
  }

  protected toggle(key: string) {
    const next = new Set(this.toggled());
    if (!next.delete(key)) {
      next.add(key);
    }
    this.toggled.set(next);
  }

  protected percent(value: number, total: number) {
    const percent = total > 0 ? Math.round((value / total) * 100) : 0;
    return percent === 0 && value > 0 ? '< 1' : String(percent);
  }

  protected color(node: FlowNode) {
    if (node.kind === 'balance') {
      return node.side === 'out' ? 'var(--z-positive)' : 'var(--z-negative)';
    }
    return node.color ?? 'var(--z-muted)';
  }

  private rows(nodes: FlowNode[], depth = 0): Row[] {
    const expanded = this.expanded();
    return nodes.flatMap((node) => {
      const open = node.children.length > 0 && expanded.has(node.key);
      return [
        { node, depth, expanded: open },
        ...(open ? this.rows(node.children, depth + 1) : []),
      ];
    });
  }

  private async load(accountId: number, range: { from?: string; to?: string }) {
    const id = ++this.requestId;
    this.loading.set(true);
    this.error.set(null);
    let params = new HttpParams();
    if (range.from) {
      params = params.set('from', range.from);
    }
    if (range.to) {
      params = params.set('to', range.to);
    }
    try {
      const flows = await firstValueFrom(
        this.http.get<Flows>(`/api/accounts/${accountId}/flows`, { params }),
      );
      if (id === this.requestId) {
        this.flows.set(flows);
      }
    } catch (e) {
      if (id === this.requestId) {
        this.error.set(errorMessage(e, 'Die Auswertung konnte nicht geladen werden.'));
      }
    } finally {
      if (id === this.requestId) {
        this.loading.set(false);
      }
    }
  }
}

/** Schlüssel aller Knoten mit Unterkategorien, in jeder Tiefe. */
function expandableKeys(nodes: FlowNode[]): string[] {
  return nodes.flatMap((node) =>
    node.children.length ? [node.key, ...expandableKeys(node.children)] : [],
  );
}

function firstOfMonth(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}
