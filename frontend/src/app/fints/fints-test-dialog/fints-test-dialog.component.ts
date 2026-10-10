import { DialogRef } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LoadingButtonDirective } from '../../buttons/loading-button.directive';
import { FinTsTestResult } from '../fints.models';
import { FinTsService } from '../fints.service';

@Component({
  selector: 'app-fints-test-dialog',
  imports: [ReactiveFormsModule, LoadingButtonDirective, DatePipe],
  templateUrl: './fints-test-dialog.component.html',
})
export class FinTsTestDialog {
  readonly dialogRef = inject(DialogRef<string>);
  private readonly finTsService = inject(FinTsService);

  readonly submitted = signal(false);
  readonly loading = signal(false);
  readonly result = signal<FinTsTestResult | null>(null);
  readonly error = signal<string | null>(null);

  readonly formGroup = new FormGroup({
    iban: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    userId: new FormControl<string>('', { nonNullable: true }),
    pin: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  formatBetrag(betrag: number) {
    return betrag.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' });
  }

  async onSubmit() {
    this.submitted.set(true);

    if (this.formGroup.invalid) {
      return;
    }

    const { iban, userId, pin } = this.formGroup.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);

    try {
      const result = await this.finTsService.test({
        iban,
        userId: userId || null,
        pin,
        days: null,
      });
      this.result.set(result);
      this.error.set(result.error);
    } catch (e) {
      const message =
        e instanceof HttpErrorResponse && typeof e.error === 'string' ? e.error : null;
      this.error.set(message ?? 'Der Abruf ist fehlgeschlagen.');
    } finally {
      this.formGroup.controls.pin.reset();
      this.loading.set(false);
    }
  }
}
