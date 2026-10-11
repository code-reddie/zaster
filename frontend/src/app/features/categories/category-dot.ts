import { Component, input } from '@angular/core';

/** Farbiger Punkt einer Kategorie; ohne Kategorie ein gestrichelter Kreis. */
@Component({
  selector: 'z-category-dot',
  host: { class: 'inline-block size-2.5 shrink-0 rounded-full', '[style]': 'style()' },
  template: ``,
})
export class CategoryDot {
  readonly color = input<string | null | undefined>();
  readonly empty = input(false);

  protected style() {
    if (this.empty()) {
      return 'border: 1.5px dashed var(--z-muted)';
    }
    return `background-color: ${this.color() ?? 'var(--z-muted)'}`;
  }
}
