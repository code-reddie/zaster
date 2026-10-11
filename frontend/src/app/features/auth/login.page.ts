import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { AuthLayout } from './auth-layout';

@Component({
  imports: [FormsModule, RouterLink, Button, AuthLayout],
  template: `
    <z-auth-layout title="Willkommen zurück" subtitle="Melde dich an, um deine Finanzen zu sehen.">
      <form class="flex flex-col gap-4" (ngSubmit)="submit()">
        <div>
          <label class="z-label" for="name">Name</label>
          <input
            id="name"
            name="name"
            class="z-input"
            autocomplete="username"
            autocapitalize="none"
            required
            [(ngModel)]="name"
          />
        </div>
        <div>
          <label class="z-label" for="password">Passwort</label>
          <input
            id="password"
            name="password"
            type="password"
            class="z-input"
            autocomplete="current-password"
            required
            [(ngModel)]="password"
          />
        </div>
        @if (error()) {
          <p class="z-error" role="alert">{{ error() }}</p>
        }
        <button zButton size="lg" type="submit" class="mt-1 w-full" [loading]="loading()">
          Anmelden
        </button>
      </form>
      <span footer>
        Noch kein Zugang?
        <a routerLink="/registrieren" class="font-medium text-accent hover:underline"
          >Registrieren</a
        >
      </span>
    </z-auth-layout>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);

  protected readonly name = signal('');
  protected readonly password = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async submit() {
    if (!this.name().trim() || !this.password()) {
      this.error.set('Bitte gib Name und Passwort ein.');
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.auth.login({ name: this.name().trim(), password: this.password() });
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Anmeldung hat nicht geklappt.'));
    } finally {
      this.loading.set(false);
    }
  }
}
