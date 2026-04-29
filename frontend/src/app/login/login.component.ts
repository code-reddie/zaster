import { Component, inject, signal } from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { AuthService } from '../authentication/auth.service';
import { Router } from '@angular/router';
import { AutoFocusDirective } from '../auto-focus.directive';
import { first, firstValueFrom } from 'rxjs';
import { LoadingButtonDirective } from '../buttons/loading-button.directive';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, AutoFocusDirective, LoadingButtonDirective],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  readonly loading = signal(false);

  readonly formGroup = new FormGroup({
    name: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    password: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  async onSubmit() {
    if (this.formGroup.invalid) {
      this.formGroup.markAllAsTouched();
      return;
    }

    const credentials = this.formGroup.getRawValue();

    this.loading.set(true);

    try {
      await firstValueFrom(this.authService.login(credentials));
      this.router.navigate(['/dashboard']);
    } catch {
      this.formGroup.setErrors({ loginFailed: true });
    } finally {
      this.loading.set(false);
    }
  }
}
