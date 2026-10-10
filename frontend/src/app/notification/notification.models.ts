export interface NotificationSettings {
  homeAssistantConfigured: boolean;
  zasterUrlConfigured: boolean;
  devices: string[];
}

export interface HomeAssistantDevice {
  service: string;
  name: string;
}

export interface NotificationDeviceResult {
  device: string;
  success: boolean;
  error: string | null;
}

export interface NotificationTestResult {
  results: NotificationDeviceResult[];
}
