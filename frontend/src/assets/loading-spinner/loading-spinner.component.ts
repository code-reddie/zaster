import { Component, input, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './loading-spinner.component.html',
})
export class LoadingSpinnerComponent {
  size = input<number>(20);
}
