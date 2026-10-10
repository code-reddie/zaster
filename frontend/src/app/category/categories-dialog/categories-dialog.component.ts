import { DialogRef } from '@angular/cdk/dialog';
import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LoadingButtonDirective } from '../../buttons/loading-button.directive';
import { TransactionStore } from '../../transaction/transaction.store';
import { RULE_FIELDS, RuleField } from '../category.models';
import { CategoryStore } from '../category.store';

@Component({
  selector: 'app-categories-dialog',
  imports: [ReactiveFormsModule, LoadingButtonDirective],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './categories-dialog.component.html',
})
export class CategoriesDialog implements OnInit {
  readonly dialogRef = inject(DialogRef<string>);
  readonly store = inject(CategoryStore);
  private readonly transactionStore = inject(TransactionStore);

  readonly ruleFields = RULE_FIELDS;
  readonly applyResult = signal<string | null>(null);
  readonly applying = signal(false);

  readonly categoryForm = new FormGroup({
    name: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    color: new FormControl<string>('#64748b', { nonNullable: true }),
  });

  readonly ruleForm = new FormGroup({
    pattern: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    field: new FormControl<RuleField>('Alle', { nonNullable: true }),
    categoryId: new FormControl<number | null>(null, { validators: [Validators.required] }),
  });

  ngOnInit() {
    this.store.loadCategories();
  }

  async addCategory() {
    if (this.categoryForm.invalid) {
      return;
    }

    const { name, color } = this.categoryForm.getRawValue();
    await this.store.createCategory({
      name,
      color,
      icon: null,
      description: null,
      parentCategoryId: null,
    });

    if (!this.store.error()) {
      this.categoryForm.reset();
    }
  }

  async deleteCategory(id: number) {
    await this.store.deleteCategory(id);
    // Buchungen dieser Kategorie sind jetzt unkategorisiert.
    await this.transactionStore.loadAllTransactions();
  }

  async addRule() {
    if (this.ruleForm.invalid) {
      return;
    }

    const { pattern, field, categoryId } = this.ruleForm.getRawValue();
    await this.store.createRule({ pattern, field, categoryId: categoryId! });

    if (!this.store.error()) {
      this.ruleForm.reset();
    }
  }

  async applyRules() {
    this.applyResult.set(null);
    this.applying.set(true);
    const updated = await this.store.applyRules();
    this.applying.set(false);
    if (updated !== null) {
      this.applyResult.set(
        updated === 1 ? '1 Buchung kategorisiert.' : `${updated} Buchungen kategorisiert.`,
      );
      await this.transactionStore.loadAllTransactions();
    }
  }

  categoryName(id: number) {
    return this.store.categoriesById().get(id)?.name ?? '?';
  }
}
