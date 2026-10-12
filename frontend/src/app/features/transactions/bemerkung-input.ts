import { Component, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAX_BEMERKUNG } from './transaction.models';

/** Eingabefeld für die Bemerkung mit Zeichenzähler, wenn es eng wird. */
@Component({
  selector: 'z-bemerkung-input',
  imports: [FormsModule],
  host: { class: 'block' },
  template: `
    <label class="z-label" [for]="inputId()">Bemerkung</label>
    <textarea
      class="z-input min-h-20 resize-y"
      rows="3"
      placeholder="Worum ging es? Z. B. „Geburtstagsgeschenk für Anna“"
      [id]="inputId()"
      [name]="inputId()"
      [maxlength]="max"
      [(ngModel)]="value"
    ></textarea>
    @if (value().length > max - 100) {
      <p class="z-hint mt-1 text-right">{{ value().length }} / {{ max }}</p>
    }
  `,
})
export class BemerkungInput {
  readonly value = model('');
  readonly inputId = input('bemerkung-text');
  protected readonly max = MAX_BEMERKUNG;
}
