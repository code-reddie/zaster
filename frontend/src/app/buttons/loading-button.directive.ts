import { Directive, effect, ElementRef, inject, input, Renderer2 } from '@angular/core';
import { getLoadingSpinnerSvg } from '../../assets/loading-spinner/loading-spinner.svg';

@Directive({
  selector: 'button[loading]',
})
export class LoadingButtonDirective {
  readonly loading = input<boolean>(false);

  private readonly elementRef = inject(ElementRef<HTMLButtonElement>);
  private readonly renderer = inject(Renderer2);

  private spinnerContainer: HTMLElement | null = null;
  private contentWrapper: HTMLElement | null = null;
  private originalChildren: ChildNode[] = [];

  showSpinnerEffect$ = effect(() => {
    if (this.loading()) {
      this.showSpinner();
    } else {
      this.hideSpinner();
    }
  });

  private showSpinner() {
    const btn = this.elementRef.nativeElement;

    this.contentWrapper = this.renderer.createElement('div');
    while (btn.firstChild) {
      this.renderer.appendChild(this.contentWrapper, btn.firstChild);
    }
    this.renderer.appendChild(btn, this.contentWrapper);

    this.renderer.setStyle(this.contentWrapper, 'opacity', '0');

    this.spinnerContainer = this.renderer.createElement('div');
    [
      'absolute',
      'inset-0',
      'flex',
      'items-center',
      'justify-center',
      'pointer-events-none',
    ].forEach((c) => this.renderer.addClass(this.spinnerContainer!, c));

    if (this.spinnerContainer) {
      this.spinnerContainer.innerHTML = getLoadingSpinnerSvg();
    }

    this.renderer.addClass(btn, 'relative');
    this.renderer.appendChild(btn, this.spinnerContainer);
  }

  private hideSpinner() {
    const btn = this.elementRef.nativeElement;

    if (this.spinnerContainer) {
      this.renderer.removeChild(btn, this.spinnerContainer);
      this.spinnerContainer = null;
    }

    if (this.contentWrapper) {
      while (this.contentWrapper.firstChild) {
        this.renderer.appendChild(btn, this.contentWrapper.firstChild);
      }
      this.renderer.removeChild(btn, this.contentWrapper);
      this.contentWrapper = null;
    }
  }
}
