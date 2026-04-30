import { DialogRef } from '@angular/cdk/dialog';
import { Component, effect, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LoadingButtonDirective } from '../../buttons/loading-button.directive';
import { AccountStore } from '../account.store';

@Component({
  selector: 'app-create-account-dialog',
  imports: [ReactiveFormsModule, LoadingButtonDirective],
  templateUrl: './create-account-dialog.component.html',
})
export class CreateAccountDialog {
  readonly dialogRef = inject(DialogRef<string>);
  readonly store = inject(AccountStore);
  readonly submitted = signal(false);

  errorEffect$ = effect(() => {
    const error = this.store.error();
    if (error) {
      this.formGroup.setErrors({ creationFailed: true });
    }
  });

  readonly formGroup = new FormGroup({
    name: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  async onSubmit() {
    this.submitted.set(true);

    if (this.formGroup.invalid) {
      return;
    }

    const newAccount = this.formGroup.getRawValue();
    this.store.createAccount(newAccount.name);

    if (!this.store.error()) {
      this.dialogRef.close();
    }
  }
}
