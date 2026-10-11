import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { AuthLayout } from './auth-layout';

const MIN_PASSWORD_LENGTH = 8;

@Component({
  imports: [FormsModule, RouterLink, Button, AuthLayout],
  template: `
    <z-auth-layout
      title="Zugang anlegen"
      subtitle="Ein Zugang pro Person. Konten lassen sich später teilen."
    >
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
            autocomplete="new-password"
            required
            [(ngModel)]="password"
          />
          <p class="z-hint">Mindestens {{ minLength }} Zeichen.</p>
        </div>
        <div>
          <label class="z-label" for="repeat">Passwort wiederholen</label>
          <input
            id="repeat"
            name="repeat"
            type="password"
            class="z-input"
            autocomplete="new-password"
            required
            [(ngModel)]="repeat"
          />
        </div>
        @if (error()) {
          <p class="z-error" role="alert">{{ error() }}</p>
        }
        <button zButton size="lg" type="submit" class="mt-1 w-full" [loading]="loading()">
          Zugang anlegen
        </button>
      </form>
      <span footer>
        Schon registriert?
        <a routerLink="/anmelden" class="font-medium text-accent hover:underline">Anmelden</a>
      </span>
    </z-auth-layout>
  `,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);

  protected readonly minLength = MIN_PASSWORD_LENGTH;
  protected readonly name = signal('');
  protected readonly password = signal('');
  protected readonly repeat = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async submit() {
    const error = !this.name().trim()
      ? 'Bitte gib einen Namen ein.'
      : this.password().length < MIN_PASSWORD_LENGTH
        ? `Das Passwort muss mindestens ${MIN_PASSWORD_LENGTH} Zeichen lang sein.`
        : this.password() !== this.repeat()
          ? 'Die Passwörter stimmen nicht überein.'
          : null;
    this.error.set(error);
    if (error) {
      return;
    }

    this.loading.set(true);
    try {
      await this.auth.register({ name: this.name().trim(), password: this.password() });
    } catch (e) {
      this.error.set(errorMessage(e, 'Der Zugang konnte nicht angelegt werden.'));
    } finally {
      this.loading.set(false);
    }
  }
}
