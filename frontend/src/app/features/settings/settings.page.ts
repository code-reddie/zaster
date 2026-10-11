import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { PageHeader } from '../../core/layout/page-header';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { Icon } from '../../core/ui/icon';
import { Spinner } from '../../core/ui/spinner';
import { ToastService } from '../../core/ui/toast';
import { AccountStore } from '../accounts/account.store';
import {
  HomeAssistantDevice,
  NotificationSettings,
  NotificationsService,
  NotificationTestResult,
} from './notifications.service';

@Component({
  imports: [FormsModule, PageHeader, Button, Icon, Spinner],
  templateUrl: './settings.page.html',
})
export class SettingsPage {
  protected readonly auth = inject(AuthService);
  private readonly service = inject(NotificationsService);
  private readonly toast = inject(ToastService);

  protected readonly settings = signal<NotificationSettings | null>(null);
  protected readonly available = signal<HomeAssistantDevice[]>([]);
  protected readonly availableError = signal<string | null>(null);
  protected readonly selected = signal<string[]>([]);
  protected readonly manualDevice = signal('');
  protected readonly saving = signal(false);
  protected readonly testing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly testResult = signal<NotificationTestResult | null>(null);

  /** Gespeicherte Geräte, die Home Assistant gerade nicht meldet, z. B. von Hand eingetragene. */
  protected readonly otherSelected = computed(() =>
    this.selected().filter((s) => !this.available().some((d) => d.service === s)),
  );

  protected readonly dirty = computed(() => {
    const saved = this.settings()?.devices ?? [];
    const selected = this.selected();
    return saved.length !== selected.length || saved.some((d) => !selected.includes(d));
  });

  constructor() {
    // Die Einstellungen gelten für den Nutzer, nicht für ein Konto.
    inject(AccountStore).selectedId.set(null);
    this.load();
  }

  protected isSelected(service: string) {
    return this.selected().includes(service);
  }

  protected toggle(service: string, checked: boolean) {
    this.selected.update((s) => (checked ? [...s, service] : s.filter((d) => d !== service)));
  }

  protected addManualDevice() {
    const device = this.manualDevice()
      .trim()
      .replace(/^notify\./, '');
    if (device && !this.selected().includes(device)) {
      this.selected.update((s) => [...s, device]);
    }
    this.manualDevice.set('');
  }

  protected async save() {
    this.saving.set(true);
    this.error.set(null);
    try {
      const settings = await this.service.updateDevices(this.selected());
      this.settings.set(settings);
      this.selected.set(settings.devices);
      this.toast.success('Geräte gespeichert.');
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Geräte konnten nicht gespeichert werden.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected async test() {
    this.testing.set(true);
    this.error.set(null);
    this.testResult.set(null);
    try {
      this.testResult.set(await this.service.sendTest());
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Testnachricht konnte nicht verschickt werden.'));
    } finally {
      this.testing.set(false);
    }
  }

  private async load() {
    try {
      const settings = await this.service.getSettings();
      this.settings.set(settings);
      this.selected.set(settings.devices);
      if (settings.homeAssistantConfigured) {
        try {
          this.available.set(await this.service.getAvailableDevices());
        } catch (e) {
          this.availableError.set(
            errorMessage(e, 'Die Geräte konnten nicht von Home Assistant geladen werden.'),
          );
        }
      }
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Einstellungen konnten nicht geladen werden.'));
    }
  }
}
