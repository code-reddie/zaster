import { Component, inject } from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { AuthService } from '../authentication/auth.service';
import { Router } from '@angular/router';
import { AutoFocusDirective } from '../auto-focus.directive';
import { firstValueFrom } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { BackendError } from '../error.models';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, AutoFocusDirective],
  templateUrl: './register.component.html',
})
export class RegisterComponent {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  readonly formGroup = new FormGroup({
    name: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3)],
    }),
    password: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(6)],
    }),
    confirmPassword: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(6)],
    }),
  });

  async onSubmit() {
    if (this.formGroup.invalid) {
      this.formGroup.markAllAsTouched();
      return;
    }

    if (this.formGroup.get('password')?.value !== this.formGroup.get('confirmPassword')?.value) {
      this.formGroup.setErrors({ passwordMismatch: true });
      return;
    }

    const credentials = this.formGroup.getRawValue();

    try {
      await firstValueFrom(this.authService.register(credentials));
      this.router.navigate(['/dashboard']);
    } catch (err: unknown) {
      if (err instanceof HttpErrorResponse) {
        const backendError = err.error as BackendError;
        if (backendError?.code === 'USER_ALREADY_EXISTS') {
          this.formGroup.setErrors({ alreadyExists: true });
          return;
        }
      }
      this.formGroup.setErrors({ registrationFailed: true });
    }
  }
}
