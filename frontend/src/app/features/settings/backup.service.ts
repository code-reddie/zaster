import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

interface Me {
  id: number;
  name: string;
  canDownloadBackup: boolean;
}

@Injectable({ providedIn: 'root' })
export class BackupService {
  private readonly http = inject(HttpClient);

  /** Nur wer Zaster eingerichtet hat, darf die Datenbank sichern. */
  async canDownload(): Promise<boolean> {
    return (await firstValueFrom(this.http.get<Me>('/api/me'))).canDownloadBackup;
  }

  /** Lädt die Datenbank und speichert sie über den Browser (ein Link reicht nicht, die Anfrage braucht den Token). */
  async download() {
    const response = await firstValueFrom(
      this.http.get('/api/backup', { observe: 'response', responseType: 'blob' }),
    );
    const name =
      /filename="?([^";]+)"?/.exec(response.headers.get('Content-Disposition') ?? '')?.[1] ??
      'zaster.db';
    const url = URL.createObjectURL(response.body!);
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
