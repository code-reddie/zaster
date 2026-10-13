import { Component, computed, inject } from '@angular/core';
import { PageHeader } from '../../core/layout/page-header';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogService } from '../../core/ui/dialog';
import { EmptyState } from '../../core/ui/empty-state';
import { Icon } from '../../core/ui/icon';
import { Spinner } from '../../core/ui/spinner';
import { ToastService } from '../../core/ui/toast';
import { AccountStore } from '../accounts/account.store';
import { assetClassLabel } from '../transactions/transaction.models';
import { CategoryDot } from './category-dot';
import { CategoryNode, Rule, RuleCondition, RULE_FIELDS } from './category.models';
import { CategorySheet, CategorySheetData } from './category-sheet';
import { CategoryStore } from './category.store';
import { RuleSheet, RuleSheetData } from './rule-sheet';

@Component({
  imports: [PageHeader, Button, Icon, Spinner, EmptyState, CategoryDot],
  templateUrl: './categories.page.html',
})
export class CategoriesPage {
  protected readonly accounts = inject(AccountStore);
  protected readonly store = inject(CategoryStore);
  private readonly dialog = inject(DialogService);
  private readonly toast = inject(ToastService);

  protected readonly rules = computed(() =>
    this.store.rules().map((rule) => ({ rule, category: this.store.byId().get(rule.categoryId) })),
  );

  protected fieldLabel(condition: RuleCondition) {
    if (condition.field === 'Anlageklasse') {
      return 'als Anlageklasse';
    }
    return condition.field === 'Alle'
      ? 'irgendwo'
      : `im ${RULE_FIELDS.find((f) => f.value === condition.field)!.label}`;
  }

  protected patternLabel(condition: RuleCondition) {
    return condition.field === 'Anlageklasse'
      ? assetClassLabel(condition.pattern)
      : condition.pattern;
  }

  /** Kurzer Zusatz in der Liste, nur wenn die Regel auf eine Richtung beschränkt ist. */
  protected directionLabel(rule: Rule) {
    return rule.direction === 'Eingang'
      ? 'nur Einnahmen'
      : rule.direction === 'Ausgang'
        ? 'nur Ausgaben'
        : null;
  }

  protected editCategory(category?: CategoryNode, parentId: number | null = null) {
    this.dialog.open<boolean, CategorySheetData>(CategorySheet, { category, parentId });
  }

  protected editRule(rule?: Rule) {
    this.dialog.open<number, RuleSheetData>(RuleSheet, { rule });
  }

  protected async moveRule(rule: Rule, direction: -1 | 1) {
    try {
      await this.store.moveRule(rule.id, direction);
    } catch (e) {
      this.toast.error(errorMessage(e, 'Die Reihenfolge konnte nicht gespeichert werden.'));
      await this.store.reload();
    }
  }

  protected async applyRules() {
    try {
      const updated = await this.store.applyRules();
      this.toast.success(
        updated ? `${updated} Buchungen per Regel zugeordnet.` : 'Keine Regel hat gepasst.',
      );
      const accountId = this.accounts.selectedId();
      if (accountId !== null) {
        this.accounts.refresh(accountId);
      }
    } catch (e) {
      this.toast.error(errorMessage(e, 'Die Regeln konnten nicht angewendet werden.'));
    }
  }
}
