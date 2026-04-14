import { Component, inject } from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { AuthService } from '../authentication/auth.service';
import { Router } from '@angular/router';
import { AutoFocusDirective } from '../auto-focus.directive';
import { first, firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, AutoFocusDirective],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

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

    try {
      await firstValueFrom(this.authService.login(credentials));
      this.router.navigate(['/dashboard']);
    } catch {
      this.formGroup.setErrors({ loginFailed: true });
    }
  }
}
