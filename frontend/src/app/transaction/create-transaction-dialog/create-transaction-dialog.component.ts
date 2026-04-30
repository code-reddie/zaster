import { DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { Component, effect, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LoadingButtonDirective } from '../../buttons/loading-button.directive';
import { TransactionStore } from '../transaction.store';

@Component({
  selector: 'app-create-transaction-dialog',
  imports: [ReactiveFormsModule, LoadingButtonDirective],
  templateUrl: './create-transaction-dialog.component.html',
})
export class CreateTransactionDialog {
  readonly dialogRef = inject(DialogRef<string>);
  readonly store = inject(TransactionStore);
  readonly submitted = signal(false);

  errorEffect$ = effect(() => {
    const error = this.store.error();
    if (error) {
      this.formGroup.setErrors({ creationFailed: true });
    }
  });

  readonly formGroup = new FormGroup({
    buchung: new FormControl<string>(new Date().toISOString().slice(0, 10), {
      nonNullable: true,
      validators: [Validators.required],
    }),
    valuta: new FormControl<string>(new Date().toISOString().slice(0, 10), {
      nonNullable: true,
      validators: [Validators.required],
    }),
    auftragsgeber: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    buchungstext: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    verwendungszweck: new FormControl<string | null>(''),
    betrag: new FormControl<number>(0, {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  async onSubmit() {
    this.submitted.set(true);

    if (this.formGroup.invalid) {
      return;
    }

    const value = this.formGroup.getRawValue();
    const accoutnId = this.store.selectedAccountId();
    if (!accoutnId) {
      this.formGroup.setErrors({ noAccountSelected: true });
      return;
    }

    await this.store.createTransaction({
      buchung: new Date(value.buchung).toISOString(),
      valuta: new Date(value.valuta).toISOString(),
      auftragsgeber: value.auftragsgeber,
      buchungstext: value.buchungstext,
      verwendungszweck: value.verwendungszweck,
      betrag: value.betrag,
      accountId: accoutnId,
    });

    if (!this.store.error()) {
      this.dialogRef.close();
    }
  }
}
