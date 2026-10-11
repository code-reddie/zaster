import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface NotificationSettings {
  homeAssistantConfigured: boolean;
  zasterUrlConfigured: boolean;
  devices: string[];
}

export interface HomeAssistantDevice {
  service: string;
  name: string;
}

export interface NotificationTestResult {
  results: { device: string; success: boolean; error: string | null }[];
}

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/me/notifications';

  getSettings(): Promise<NotificationSettings> {
    return firstValueFrom(this.http.get<NotificationSettings>(this.url));
  }

  getAvailableDevices(): Promise<HomeAssistantDevice[]> {
    return firstValueFrom(this.http.get<HomeAssistantDevice[]>(`${this.url}/available-devices`));
  }

  updateDevices(devices: string[]): Promise<NotificationSettings> {
    return firstValueFrom(this.http.put<NotificationSettings>(`${this.url}/devices`, { devices }));
  }

  sendTest(): Promise<NotificationTestResult> {
    return firstValueFrom(this.http.post<NotificationTestResult>(`${this.url}/test`, {}));
  }
}
