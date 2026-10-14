import {
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FlowTree } from './flow.models';
import { LaidOutNode, layoutSankey } from './sankey-layout';

const euro = new Intl.NumberFormat('de-DE', {
  style: 'currency',
  currency: 'EUR',
  maximumFractionDigits: 0,
});
const euroExact = new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' });

/** Ungefähre Breite eines Zeichens bei 12px, um lange Namen zu kürzen. */
const CHAR_WIDTH = 7;

/**
 * Sankey-Diagramm als SVG: Einnahmen links, Konto in der Mitte, Ausgaben rechts.
 * Ein Klick auf eine Kategorie mit Unterkategorien klappt sie auf oder zu.
 */
@Component({
  selector: 'z-sankey-chart',
  host: { class: 'block overflow-x-auto overscroll-x-contain' },
  template: `
    @if (layout(); as l) {
      <svg
        [attr.width]="l.width"
        [attr.height]="l.height"
        [attr.viewBox]="'0 0 ' + l.width + ' ' + l.height"
        role="img"
        [attr.aria-label]="ariaLabel()"
        class="block select-none"
      >
        <g>
          @for (link of l.links; track link.key) {
            <path
              [attr.d]="link.path"
              [style.fill]="link.color ?? 'var(--z-muted)'"
              class="opacity-30 transition-opacity hover:opacity-55"
            >
              <title>{{ link.title }}: {{ exact(link.value) }}</title>
            </path>
          }
        </g>
        @for (node of l.nodes; track node.key) {
          <g
            [class.cursor-pointer]="node.expandable"
            (click)="node.expandable && toggle.emit(node.key)"
          >
            <title>{{ node.title }}: {{ exact(node.value) }}</title>
            <rect
              [attr.x]="node.x"
              [attr.y]="node.y"
              [attr.width]="l.nodeWidth"
              [attr.height]="node.h"
              rx="2"
              [style.fill]="nodeColor(node)"
            />
            @if (node.side === 'center') {
              <text
                [attr.x]="node.x + l.nodeWidth / 2"
                [attr.y]="node.labelY"
                text-anchor="middle"
                class="halo fill-fg text-[13px] font-semibold"
              >
                {{ node.label }} · {{ short(node.value) }}
              </text>
            } @else {
              <text
                [attr.x]="node.side === 'in' ? node.x - 6 : node.x + l.nodeWidth + 6"
                [attr.y]="node.labelY"
                [attr.text-anchor]="node.side === 'in' ? 'end' : 'start'"
                class="halo"
              >
                <tspan [attr.dy]="-2" class="fill-fg text-xs font-medium">
                  @if (node.expandable && node.side === 'in') {
                    <tspan class="fill-accent font-bold">{{ node.expanded ? '−' : '+' }}</tspan>
                  }
                  {{ fit(node.label, node.expandable) }}
                  @if (node.expandable && node.side === 'out') {
                    <tspan class="fill-accent font-bold">{{ node.expanded ? '−' : '+' }}</tspan>
                  }
                </tspan>
                <tspan
                  [attr.x]="node.side === 'in' ? node.x - 6 : node.x + l.nodeWidth + 6"
                  dy="13"
                  class="amount fill-muted text-[11px]"
                >
                  {{ short(node.value) }}
                </tspan>
              </text>
            }
          </g>
        }
      </svg>
    }
  `,
  styles: `
    .halo {
      paint-order: stroke;
      stroke: var(--z-surface);
      stroke-width: 3px;
      stroke-linejoin: round;
    }
  `,
})
export class SankeyChart {
  readonly tree = input.required<FlowTree>();
  readonly centerLabel = input.required<string>();
  readonly expanded = input.required<ReadonlySet<string>>();
  readonly toggle = output<string>();

  private readonly width = signal(0);

  /** Am iPad (hochkant) etwas schmaler, damit zwei aufgeklappte Ebenen ohne Scrollen passen. */
  private readonly labelWidth = computed(() => {
    const width = this.width();
    return width < 640 ? 96 : width < 1000 ? 120 : 160;
  });

  protected readonly layout = computed(() => {
    const width = this.width();
    if (!width) {
      return null;
    }
    return layoutSankey(this.tree(), this.centerLabel(), this.expanded(), {
      width,
      labelWidth: this.labelWidth(),
      height: width < 640 ? 300 : 420,
    });
  });

  protected readonly ariaLabel = computed(() => {
    const tree = this.tree();
    return `Einnahmen ${this.exact(tree.einnahmen)}, Ausgaben ${this.exact(tree.ausgaben)}`;
  });

  constructor() {
    const host = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;
    const observer = new ResizeObserver(() => this.width.set(Math.floor(host.clientWidth)));
    observer.observe(host);
    inject(DestroyRef).onDestroy(() => observer.disconnect());
  }

  protected nodeColor(node: LaidOutNode) {
    if (node.side === 'center') {
      return 'var(--z-accent)';
    }
    if (node.node?.kind === 'balance') {
      return node.side === 'out' ? 'var(--z-positive)' : 'var(--z-negative)';
    }
    return node.color ?? 'var(--z-muted)';
  }

  /** Kürzt Namen, die nicht neben den Knoten passen. */
  protected fit(label: string, expandable: boolean) {
    const max = Math.floor((this.labelWidth() - 16) / CHAR_WIDTH) - (expandable ? 2 : 0);
    return label.length > max ? `${label.slice(0, max - 1)}…` : label;
  }

  protected short(value: number) {
    return euro.format(value);
  }

  protected exact(value: number) {
    return euroExact.format(value);
  }
}
