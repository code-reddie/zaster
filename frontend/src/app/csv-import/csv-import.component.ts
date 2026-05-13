import { Component, output, signal } from '@angular/core';

@Component({
  selector: 'app-csv-import',
  templateUrl: './csv-import.component.html',
})
export class CsvImportComponent {
  fileSelected = output<File>();
  isDragging = signal(false);
  error = signal<string | null>(null);

  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragging.set(true);
  }

  onDragLeave() {
    this.isDragging.set(false);
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragging.set(false);
    const file = event.dataTransfer?.files[0];
    this.handleFile(file);
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.handleFile(input.files?.[0]);
  }

  openFileDialog() {
    document.getElementById('csvFileInput')?.click();
  }

  private handleFile(file: File | undefined) {
    if (!file) return;

    if (!file.name.endsWith('.csv')) {
      this.error.set('Bitte nur CSV-Dateien importieren.');
      return;
    }

    this.error.set(null);
    this.fileSelected.emit(file);
  }
}
