import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { PageHeader } from '../../core/layout/page-header';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { Icon } from '../../core/ui/icon';
import { Spinner } from '../../core/ui/spinner';
import { ToastService } from '../../core/ui/toast';
import { IbanPipe } from '../../shared/format';
import { AccountStore } from '../accounts/account.store';
import { BackupSchedule, BackupService } from './backup.service';
import {
  HomeAssistantDevice,
  NotificationSettings,
  NotificationsService,
  NotificationTestResult,
} from './notifications.service';

@Component({
  imports: [DatePipe, FormsModule, PageHeader, Button, Icon, Spinner, IbanPipe],
  templateUrl: './settings.page.html',
})
export class SettingsPage {
  protected readonly auth = inject(AuthService);
  protected readonly accounts = inject(AccountStore);
  private readonly service = inject(NotificationsService);
  private readonly toast = inject(ToastService);
  private readonly backup = inject(BackupService);

  protected readonly settings = signal<NotificationSettings | null>(null);
  protected readonly available = signal<HomeAssistantDevice[]>([]);
  protected readonly availableError = signal<string | null>(null);
  protected readonly selected = signal<string[]>([]);
  protected readonly manualDevice = signal('');
  protected readonly saving = signal(false);
  protected readonly testing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly testResult = signal<NotificationTestResult | null>(null);
  protected readonly canBackup = signal(false);
  protected readonly downloading = signal(false);
  protected readonly backupError = signal<string | null>(null);
  protected readonly schedule = signal<BackupSchedule | null>(null);
  protected readonly scheduleError = signal<string | null>(null);
  protected readonly backupTime = signal('');
  protected readonly savingSchedule = signal(false);
  protected readonly running = signal(false);

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
    this.accounts.selectedId.set(null);
    this.load();
    this.backup.canDownload().then(
      (can) => {
        this.canBackup.set(can);
        if (can) {
          this.loadSchedule();
        }
      },
      () => this.canBackup.set(false),
    );
  }

  protected async moveAccount(id: number, direction: -1 | 1) {
    try {
      await this.accounts.move(id, direction);
    } catch (e) {
      this.toast.error(errorMessage(e, 'Die Reihenfolge konnte nicht gespeichert werden.'));
      await this.accounts.load();
    }
  }

  protected async saveSchedule() {
    this.savingSchedule.set(true);
    this.scheduleError.set(null);
    try {
      this.setSchedule(await this.backup.saveSchedule(this.backupTime() || null));
      this.toast.success('Uhrzeit gespeichert.');
    } catch (e) {
      this.scheduleError.set(errorMessage(e, 'Die Uhrzeit konnte nicht gespeichert werden.'));
    } finally {
      this.savingSchedule.set(false);
    }
  }

  protected async runBackup() {
    this.running.set(true);
    this.scheduleError.set(null);
    try {
      const schedule = await this.backup.runNow();
      this.setSchedule(schedule);
      if (!schedule.lastError) {
        this.toast.success('Sicherung abgelegt.');
      }
    } catch (e) {
      this.scheduleError.set(errorMessage(e, 'Die Sicherung konnte nicht erstellt werden.'));
    } finally {
      this.running.set(false);
    }
  }

  private async loadSchedule() {
    try {
      this.setSchedule(await this.backup.getSchedule());
    } catch (e) {
      this.scheduleError.set(
        errorMessage(e, 'Die automatische Sicherung konnte nicht geladen werden.'),
      );
    }
  }

  private setSchedule(schedule: BackupSchedule) {
    this.schedule.set(schedule);
    this.backupTime.set(schedule.time ?? '');
  }

  protected async downloadBackup() {
    this.downloading.set(true);
    this.backupError.set(null);
    try {
      await this.backup.download();
    } catch (e) {
      this.backupError.set(errorMessage(e, 'Die Sicherung konnte nicht erstellt werden.'));
    } finally {
      this.downloading.set(false);
    }
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
