import { DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AccountService } from '../account.service';
import { LoadingButtonDirective } from '../../buttons/loading-button.directive';

@Component({
  selector: 'app-create-account-dialog',
  imports: [ReactiveFormsModule, LoadingButtonDirective],
  templateUrl: './create-account-dialog.component.html',
})
export class CreateAccountDialog {
  readonly dialogRef = inject(DialogRef<string>);
  private readonly accountService = inject(AccountService);
  readonly loading = signal(false);

  readonly formGroup = new FormGroup({
    name: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  async onSubmit() {
    if (this.formGroup.invalid) {
      this.formGroup.markAllAsTouched();
      return;
    }

    const newAccount = this.formGroup.getRawValue();

    this.loading.set(true);

    try {
      await firstValueFrom(this.accountService.createAccount(newAccount.name));
      this.dialogRef.close();
    } catch (err: unknown) {
      console.error('Error creating account:', err);
      this.formGroup.setErrors({ creationFailed: true });
    } finally {
      this.loading.set(false);
    }
  }
}
