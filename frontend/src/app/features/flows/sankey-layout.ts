import { FlowNode, FlowSide, FlowTree } from './flow.models';

export interface SankeyOptions {
  /** Verfügbare Breite; reicht sie nicht, wird das Diagramm breiter (waagerecht scrollen). */
  width: number;
  /** Platz für Beschriftungen links bzw. rechts neben einer Spalte. */
  labelWidth: number;
  /** Höhe, auf die der Gesamtbetrag ungefähr kommt. */
  height: number;
  nodeWidth?: number;
  /** Abstand zwischen Knoten einer Spalte. */
  padding?: number;
  /** Mindesthöhe je Knoten, damit Name und Betrag lesbar bleiben. */
  minSlot?: number;
}

export interface LaidOutNode {
  node: FlowNode | null;
  key: string;
  label: string;
  title: string;
  value: number;
  color: string | null;
  side: FlowSide | 'center';
  x: number;
  y: number;
  h: number;
  /** Mitte des Platzes, den der Knoten belegt; dort steht die Beschriftung. */
  labelY: number;
  expandable: boolean;
  expanded: boolean;
}

export interface LaidOutLink {
  key: string;
  path: string;
  color: string | null;
  title: string;
  value: number;
}

export interface SankeyLayout {
  width: number;
  height: number;
  nodeWidth: number;
  nodes: LaidOutNode[];
  links: LaidOutLink[];
}

const TOP = 28;
const MIN_BAR = 1.5;

/**
 * Ordnet die Knoten in Spalten an: in der Mitte das Konto, links die Einnahmen, rechts die
 * Ausgaben, aufgeklappte Unterkategorien jeweils eine Spalte weiter außen. Knotenhöhe und
 * Breite der Bänder entsprechen dem Betrag. Unterkategorien stehen möglichst mittig neben
 * ihrer Elternkategorie und rutschen nach unten, wenn darüber schon etwas steht.
 */
export function layoutSankey(
  tree: FlowTree,
  centerLabel: string,
  expanded: ReadonlySet<string>,
  options: SankeyOptions,
): SankeyLayout {
  const nodeWidth = options.nodeWidth ?? 10;
  const padding = options.padding ?? 8;
  const minSlot = options.minSlot ?? 30;
  // Durch den Ausgleichsknoten sind beide Seiten gleich groß.
  const total = Math.max(sumValues(tree.income), sumValues(tree.expense), 0.01);
  const scale = options.height / total;

  const incomeColumns = columns(tree.income, expanded);
  const expenseColumns = columns(tree.expense, expanded);
  const depthIn = incomeColumns.length;
  const depthOut = expenseColumns.length;

  // Waagerecht: Außen Platz für Beschriftungen, zwischen zwei Spalten einer Seite ebenfalls
  // (die Beschriftung der inneren Spalte steht außen neben ihr), neben dem Konto nur die Bänder.
  const gapCount = depthIn + depthOut;
  const minGaps: number[] = [];
  for (let i = 0; i < gapCount; i++) {
    const besideCenter = i === depthIn - 1 || i === depthIn;
    minGaps.push(besideCenter ? 56 : options.labelWidth + 32);
  }
  const marginLeft = depthIn ? options.labelWidth : 16;
  const marginRight = depthOut ? options.labelWidth : 16;
  const minWidth = marginLeft + marginRight + nodeWidth + minGaps.reduce((a, b) => a + b, 0);
  const width = Math.max(options.width, minWidth);
  const extra = gapCount ? (width - minWidth) / gapCount : 0;
  const columnX: number[] = [marginLeft];
  for (const gap of minGaps) {
    columnX.push(columnX.at(-1)! + gap + extra);
  }
  // Ohne Spalten (keine Buchungen) steht das Konto in der Mitte.
  if (!gapCount) {
    columnX[0] = (width - nodeWidth) / 2;
  }

  const nodes: LaidOutNode[] = [];
  const links: LaidOutLink[] = [];
  const placed = new Map<string, LaidOutNode>();

  const placeSide = (side: FlowSide, sideColumns: FlowNode[][]) => {
    sideColumns.forEach((column, depth) => {
      const x = side === 'in' ? columnX[depthIn - 1 - depth] : columnX[depthIn + 1 + depth];
      let bottom = TOP - padding;
      // Spalte 0 beginnt oben, tiefere Spalten gruppiert nach Elternknoten.
      const groups =
        depth === 0 ? [{ parent: null, items: column }] : groupByParent(column, placed);
      for (const { parent, items } of groups) {
        const slots = items.map((n) => Math.max(barHeight(n.value, scale), minSlot));
        const block = slots.reduce((a, b) => a + b, 0) + padding * (items.length - 1);
        const desired = parent ? parent.labelY - block / 2 : TOP;
        let y = Math.max(desired, bottom + padding, TOP);
        items.forEach((item, i) => {
          const h = barHeight(item.value, scale);
          const laidOut: LaidOutNode = {
            node: item,
            key: item.key,
            label: item.label,
            title: item.title,
            value: item.value,
            color: item.color,
            side,
            x,
            y: y + (slots[i] - h) / 2,
            h,
            labelY: y + slots[i] / 2,
            expandable: item.children.length > 0,
            expanded: item.children.length > 0 && expanded.has(item.key),
          };
          nodes.push(laidOut);
          placed.set(item.key, laidOut);
          y += slots[i] + padding;
        });
        bottom = y - padding;
      }
    });
  };

  placeSide('in', incomeColumns);
  placeSide('out', expenseColumns);

  // Die kürzere innere Spalte rückt samt ihren Unterkategorien in die Mitte der längeren,
  // das Konto steht mittig dazwischen.
  const extent = (roots: FlowNode[]) =>
    roots.length
      ? Math.max(
          ...roots.map((r) => placed.get(r.key)!).map((n) => n.labelY + Math.max(n.h, minSlot) / 2),
        )
      : TOP + 40;
  const bottomIn = extent(tree.income);
  const bottomOut = extent(tree.expense);
  const bottom = Math.max(bottomIn, bottomOut);
  for (const [side, sideBottom] of [
    ['in', bottomIn],
    ['out', bottomOut],
  ] as const) {
    const shift = (bottom - sideBottom) / 2;
    for (const node of nodes.filter((n) => n.side === side)) {
      node.y += shift;
      node.labelY += shift;
    }
  }
  const top = TOP;
  const centerHeight = Math.max(barHeight(total, scale), MIN_BAR);
  const center: LaidOutNode = {
    node: null,
    key: 'konto',
    label: centerLabel,
    title: centerLabel,
    value: total,
    color: null,
    side: 'center',
    x: columnX[depthIn],
    y: Math.max(TOP, (top + bottom) / 2 - centerHeight / 2),
    h: centerHeight,
    labelY: 0,
    expandable: false,
    expanded: false,
  };
  center.labelY = center.y - 12;
  nodes.push(center);

  // Bänder: Einnahmen fließen von außen zum Konto, Ausgaben vom Konto nach außen.
  // An der inneren Seite liegen die Bänder lückenlos übereinander.
  const connect = (source: LaidOutNode, targets: FlowNode[], side: FlowSide) => {
    let offset = source.y;
    for (const target of targets) {
      const laidOut = placed.get(target.key)!;
      const w = barHeight(target.value, scale);
      if (side === 'out') {
        links.push(
          link(source.x + nodeWidth, offset, laidOut.x, laidOut.y, w, laidOut, source.label),
        );
      } else {
        links.push(
          link(laidOut.x + nodeWidth, laidOut.y, source.x, offset, w, laidOut, source.label, true),
        );
      }
      offset += w;
      if (laidOut.expanded) {
        connect(laidOut, target.children, side);
      }
    }
  };
  connect(center, tree.income, 'in');
  connect(center, tree.expense, 'out');

  const height = Math.max(...nodes.map((n) => Math.max(n.y + n.h, n.labelY + minSlot / 2))) + 8;
  return { width, height, nodeWidth, nodes, links };
}

/** Sichtbare Knoten je Spalte, von innen nach außen. */
function columns(roots: FlowNode[], expanded: ReadonlySet<string>): FlowNode[][] {
  const result: FlowNode[][] = [];
  let current = roots;
  while (current.length) {
    result.push(current);
    current = current.flatMap((n) => (expanded.has(n.key) ? n.children : []));
  }
  return result;
}

function groupByParent(column: FlowNode[], placed: Map<string, LaidOutNode>) {
  const groups: { parent: LaidOutNode | null; items: FlowNode[] }[] = [];
  for (const parent of placed.values()) {
    const items = parent.node!.children.filter((c) => column.includes(c));
    if (items.length) {
      groups.push({ parent, items });
    }
  }
  return groups;
}

function link(
  x0: number,
  y0: number,
  x1: number,
  y1: number,
  w: number,
  outer: LaidOutNode,
  centerLabel: string,
  incoming = false,
): LaidOutLink {
  const xm = (x0 + x1) / 2;
  const path =
    `M${x0},${y0}C${xm},${y0} ${xm},${y1} ${x1},${y1}` +
    `L${x1},${y1 + w}C${xm},${y1 + w} ${xm},${y0 + w} ${x0},${y0 + w}Z`;
  return {
    key: `${outer.key}->`,
    path,
    color: outer.color,
    title: incoming ? `${outer.title} → ${centerLabel}` : `${centerLabel} → ${outer.title}`,
    value: outer.value,
  };
}

function barHeight(value: number, scale: number) {
  return Math.max(value * scale, MIN_BAR);
}

function sumValues(nodes: FlowNode[]) {
  return nodes.reduce((a, n) => a + n.value, 0);
}
