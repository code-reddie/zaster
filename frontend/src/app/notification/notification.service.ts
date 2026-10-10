import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  HomeAssistantDevice,
  NotificationSettings,
  NotificationTestResult,
} from './notification.models';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);

  getSettings() {
    return firstValueFrom(this.http.get<NotificationSettings>('/api/notification/settings'));
  }

  getAvailableDevices() {
    return firstValueFrom(
      this.http.get<HomeAssistantDevice[]>('/api/notification/available-devices'),
    );
  }

  updateDevices(devices: string[]) {
    return firstValueFrom(
      this.http.put<NotificationSettings>('/api/notification/devices', { devices }),
    );
  }

  sendTest() {
    return firstValueFrom(this.http.post<NotificationTestResult>('/api/notification/test', {}));
  }
}
