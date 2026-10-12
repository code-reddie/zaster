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
import { CategoryDot } from './category-dot';
import { CategoryNode, compareText, Rule, RULE_FIELDS } from './category.models';
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

  /** Alphabetisch nach Suchtext, bei gleichem Suchtext nach Kategorie. */
  protected readonly rules = computed(() =>
    this.store
      .rules()
      .map((rule) => ({ rule, category: this.store.byId().get(rule.categoryId) }))
      .sort(
        (a, b) =>
          compareText(a.rule.pattern, b.rule.pattern) ||
          compareText(a.category?.path ?? '', b.category?.path ?? ''),
      ),
  );

  protected fieldLabel(rule: Rule) {
    return rule.field === 'Alle'
      ? 'irgendwo'
      : `im ${RULE_FIELDS.find((f) => f.value === rule.field)!.label}`;
  }

  protected editCategory(category?: CategoryNode, parentId: number | null = null) {
    this.dialog.open<boolean, CategorySheetData>(CategorySheet, { category, parentId });
  }

  protected editRule(rule?: Rule) {
    this.dialog.open<number, RuleSheetData>(RuleSheet, { rule });
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
