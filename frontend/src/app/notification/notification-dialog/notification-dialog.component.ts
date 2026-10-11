import { DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LoadingButtonDirective } from '../../buttons/loading-button.directive';
import {
  HomeAssistantDevice,
  NotificationSettings,
  NotificationTestResult,
} from '../notification.models';
import { NotificationService } from '../notification.service';

@Component({
  selector: 'app-notification-dialog',
  imports: [FormsModule, LoadingButtonDirective],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './notification-dialog.component.html',
})
export class NotificationDialog {
  readonly dialogRef = inject(DialogRef<string>);
  private readonly notificationService = inject(NotificationService);

  readonly settings = signal<NotificationSettings | null>(null);
  readonly availableDevices = signal<HomeAssistantDevice[]>([]);
  readonly availableDevicesError = signal<string | null>(null);
  readonly selected = signal<string[]>([]);
  readonly manualDevice = signal('');

  readonly saving = signal(false);
  readonly testing = signal(false);
  readonly error = signal<string | null>(null);
  readonly testResult = signal<NotificationTestResult | null>(null);

  /** Gespeicherte Geräte, die Home Assistant (gerade) nicht meldet, z. B. von Hand eingetragene. */
  readonly otherSelected = computed(() =>
    this.selected().filter((s) => !this.availableDevices().some((d) => d.service === s)),
  );

  readonly dirty = computed(() => {
    const saved = this.settings()?.devices ?? [];
    const selected = this.selected();
    return saved.length !== selected.length || saved.some((d) => !selected.includes(d));
  });

  constructor() {
    this.load();
  }

  private async load() {
    try {
      const settings = await this.notificationService.getSettings();
      this.settings.set(settings);
      this.selected.set(settings.devices);
      if (settings.homeAssistantConfigured) {
        await this.loadAvailableDevices();
      }
    } catch {
      this.error.set('Die Einstellungen konnten nicht geladen werden.');
    }
  }

  private async loadAvailableDevices() {
    try {
      this.availableDevices.set(await this.notificationService.getAvailableDevices());
      this.availableDevicesError.set(null);
    } catch (e) {
      this.availableDevicesError.set(
        errorMessage(e) ?? 'Die Geräte konnten nicht von Home Assistant geladen werden.',
      );
    }
  }

  isSelected(service: string) {
    return this.selected().includes(service);
  }

  toggle(service: string, checked: boolean) {
    this.selected.update((s) => (checked ? [...s, service] : s.filter((d) => d !== service)));
  }

  addManualDevice() {
    const device = this.manualDevice()
      .trim()
      .replace(/^notify\./, '');
    if (device && !this.selected().includes(device)) {
      this.selected.update((s) => [...s, device]);
    }
    this.manualDevice.set('');
  }

  async onSave() {
    this.saving.set(true);
    this.error.set(null);
    try {
      const settings = await this.notificationService.updateDevices(this.selected());
      this.settings.set(settings);
      this.selected.set(settings.devices);
    } catch (e) {
      this.error.set(errorMessage(e) ?? 'Die Geräte konnten nicht gespeichert werden.');
    } finally {
      this.saving.set(false);
    }
  }

  async onTest() {
    this.testing.set(true);
    this.error.set(null);
    this.testResult.set(null);
    try {
      this.testResult.set(await this.notificationService.sendTest());
    } catch (e) {
      this.error.set(errorMessage(e) ?? 'Die Testnachricht konnte nicht verschickt werden.');
    } finally {
      this.testing.set(false);
    }
  }
}

function errorMessage(e: unknown) {
  return e instanceof HttpErrorResponse && typeof e.error === 'string' ? e.error : null;
}
