import { Directive, ElementRef, inject, AfterViewInit } from '@angular/core';

@Directive({
  selector: '[appAutoFocus]',
  standalone: true,
})
export class AutoFocusDirective implements AfterViewInit {
  private elementRef = inject(ElementRef);

  ngAfterViewInit() {
    requestAnimationFrame(() => {
      this.elementRef.nativeElement.focus();
    });
  }
}
