/**
 * Where a dragged box lands.
 *
 * One rule for every drag on the canvas, whether an element is moved or one
 * of its edges is pulled:
 *
 * 1. Per axis, the dragged side goes to the nearest line of another element
 *    that is within reach. Reach is a distance on screen, so it feels the
 *    same at every zoom.
 * 2. With no line in reach, it goes to the grid. The grid is fixed on the
 *    canvas; it does not depend on zoom or on which way the drag started.
 * 3. Results are whole canvas units.
 * 4. A guide is drawn for what is true of the final box, never for what was
 *    only close.
 *
 * Nothing here reads a store or the DOM.
 */

import {
  CANVAS_CENTER_X,
  CANVAS_CENTER_Y,
  calculateBounds,
  type ElementBounds,
  type GuideLine,
  type SizeMatchGuide,
  type SpacingGuide,
} from './smartGuides';

/** Screen pixels within which an edge is pulled to another element's line. */
export const SNAP_REACH_PX = 5;
/** Screen pixels within which a size is pulled to another element's size. */
export const SIZE_REACH_PX = 4;

export interface SnapOptions {
  /** Reach in canvas units, for lines and equal gaps. */
  reach: number;
  /** Reach in canvas units, for matching another element's size. */
  sizeReach: number;
  /** Grid step in canvas units. */
  grid: number;
  alignment: boolean;
  spacing: boolean;
  sizeMatch: boolean;
  /** Nothing pulls; the result is only rounded to whole units. */
  suspended: boolean;
}

export function snapOptionsFor(input: {
  zoom: number;
  grid?: number;
  alignment?: boolean;
  spacing?: boolean;
  sizeMatch?: boolean;
  suspended?: boolean;
}): SnapOptions {
  const zoom = input.zoom > 0 ? input.zoom : 1;
  return {
    reach: SNAP_REACH_PX / zoom,
    sizeReach: SIZE_REACH_PX / zoom,
    grid: Math.max(1, Math.round(input.grid ?? 5)),
    alignment: input.alignment !== false,
    spacing: input.spacing !== false,
    sizeMatch: input.sizeMatch !== false,
    suspended: input.suspended === true,
  };
}

type Axis = 'x' | 'y';

/** A box seen along one axis. `cross` is its extent on the other axis. */
interface Span {
  id: string;
  lo: number;
  hi: number;
  mid: number;
  size: number;
  crossLo: number;
  crossHi: number;
}

const EPSILON = 1e-6;
/** Whole units cannot do better than half a unit, so that much counts. */
const TRUE_WITHIN = 0.5 + EPSILON;
const GUIDE_OVERHANG = 12;
const CANVAS_GUIDE_OVERHANG = 500;
const LONGEST_GAP = 500;

const spanOf = (box: ElementBounds, axis: Axis): Span =>
  axis === 'x'
    ? {
        id: box.id,
        lo: box.left,
        hi: box.right,
        mid: box.centerX,
        size: box.width,
        crossLo: box.top,
        crossHi: box.bottom,
      }
    : {
        id: box.id,
        lo: box.top,
        hi: box.bottom,
        mid: box.centerY,
        size: box.height,
        crossLo: box.left,
        crossHi: box.right,
      };

const sharesCross = (a: Span, b: Span) =>
  a.crossHi > b.crossLo && a.crossLo < b.crossHi;

/** Keeps the nearest offer that is within its reach. */
class Nearest {
  delta: number | null = null;
  offer(delta: number, reach: number) {
    if (Math.abs(delta) > reach + EPSILON) return;
    if (this.delta === null || Math.abs(delta) < Math.abs(this.delta)) {
      this.delta = delta;
    }
  }
}

const toGrid = (value: number, grid: number) => Math.round(value / grid) * grid;

// Equal gaps ------------------------------------------------------------------

interface Gap {
  from: Span;
  to: Span;
  size: number;
}

interface Row {
  /** The element just before the box on this axis, if any. */
  before: Span | null;
  after: Span | null;
  /** Gaps between neighbours that already stand in the box's row. */
  gaps: Gap[];
}

/**
 * The elements the box stands in line with. `slack` lets a neighbour count
 * while the raw box still overlaps it a little.
 */
function rowOf(box: Span, targets: Span[], slack: number): Row {
  const row = targets
    .filter((target) => sharesCross(target, box))
    .sort((a, b) => a.lo - b.lo);

  let before: Span | null = null;
  let after: Span | null = null;
  for (const target of row) {
    if (target.hi <= box.lo + slack && target.mid < box.mid) {
      if (!before || target.hi > before.hi) before = target;
    } else if (target.lo >= box.hi - slack && target.mid > box.mid) {
      if (!after || target.lo < after.lo) after = target;
    }
  }

  const gaps: Gap[] = [];
  for (let index = 0; index < row.length - 1; index += 1) {
    const from = row[index];
    const to = row[index + 1];
    // The room the box itself stands in is not a gap to copy.
    if (from === before && to === after) continue;
    const size = to.lo - from.hi;
    if (size > 0 && size < LONGEST_GAP && sharesCross(from, to)) {
      gaps.push({ from, to, size });
    }
  }
  return { before, after, gaps };
}

/** Places for the box's low side that repeat a gap or sit in the middle. */
function equalGapPlaces(box: Span, targets: Span[], reach: number): number[] {
  const { before, after, gaps } = rowOf(box, targets, reach);
  const places: number[] = [];
  for (const gap of gaps) {
    if (before) places.push(before.hi + gap.size);
    if (after) places.push(after.lo - gap.size - box.size);
  }
  if (before && after) {
    const room = after.lo - before.hi - box.size;
    if (room > 0) places.push(before.hi + room / 2);
  }
  return places;
}

// Moving ----------------------------------------------------------------------

function settleMove(
  box: Span,
  targets: Span[],
  canvasCenter: number,
  options: SnapOptions,
): number {
  if (options.suspended) return Math.round(box.lo);

  const nearest = new Nearest();
  if (options.alignment) {
    nearest.offer(canvasCenter - box.mid, options.reach);
    for (const target of targets) {
      nearest.offer(target.lo - box.lo, options.reach);
      nearest.offer(target.hi - box.hi, options.reach);
      nearest.offer(target.mid - box.mid, options.reach);
      // Side by side with no gap.
      nearest.offer(target.hi - box.lo, options.reach);
      nearest.offer(target.lo - box.hi, options.reach);
    }
  }
  if (options.spacing) {
    for (const place of equalGapPlaces(box, targets, options.reach)) {
      nearest.offer(place - box.lo, options.reach);
    }
  }

  return nearest.delta !== null
    ? Math.round(box.lo + nearest.delta)
    : toGrid(box.lo, options.grid);
}

const lineNames: Record<
  Axis,
  [GuideLine['alignType'], GuideLine['alignType'], GuideLine['alignType']]
> = {
  x: ['left', 'center', 'right'],
  y: ['top', 'middle', 'bottom'],
};

/** Collects guide lines, one per position, each as long as what it joins. */
class Lines {
  private byPosition = new Map<string, GuideLine>();
  constructor(private axis: Axis) {}

  add(
    position: number,
    alignType: GuideLine['alignType'],
    from: number,
    to: number,
  ) {
    const key = position.toFixed(2);
    const known = this.byPosition.get(key);
    if (known) {
      known.start = Math.min(known.start ?? from, from);
      known.end = Math.max(known.end ?? to, to);
      return;
    }
    this.byPosition.set(key, {
      // A line at an x position runs vertically.
      type: this.axis === 'x' ? 'vertical' : 'horizontal',
      position,
      alignType,
      start: from,
      end: to,
    });
  }

  join(position: number, alignType: GuideLine['alignType'], a: Span, b: Span) {
    this.add(
      position,
      alignType,
      Math.min(a.crossLo, b.crossLo) - GUIDE_OVERHANG,
      Math.max(a.crossHi, b.crossHi) + GUIDE_OVERHANG,
    );
  }

  all() {
    return [...this.byPosition.values()];
  }
}

const meets = (a: number, b: number) => Math.abs(a - b) <= TRUE_WITHIN;

function moveLines(
  axis: Axis,
  box: Span,
  targets: Span[],
  canvasCenter: number,
): GuideLine[] {
  const [low, middle, high] = lineNames[axis];
  const lines = new Lines(axis);
  for (const target of targets) {
    if (meets(box.lo, target.lo)) lines.join(target.lo, low, box, target);
    if (meets(box.hi, target.hi)) lines.join(target.hi, high, box, target);
    if (meets(box.mid, target.mid)) lines.join(target.mid, middle, box, target);
    if (meets(box.lo, target.hi)) lines.join(target.hi, low, box, target);
    if (meets(box.hi, target.lo)) lines.join(target.lo, high, box, target);
  }
  if (meets(box.mid, canvasCenter)) {
    lines.add(
      canvasCenter,
      middle,
      box.crossLo - CANVAS_GUIDE_OVERHANG,
      box.crossHi + CANVAS_GUIDE_OVERHANG,
    );
  }
  return lines.all();
}

function gapGuide(axis: Axis, from: Span, to: Span): SpacingGuide {
  const overlapLo = Math.max(from.crossLo, to.crossLo);
  const overlapHi = Math.min(from.crossHi, to.crossHi);
  return {
    type: 'spacing',
    direction: axis === 'x' ? 'horizontal' : 'vertical',
    value: to.lo - from.hi,
    startPos: from.hi,
    endPos: to.lo,
    crossAxisPos:
      overlapHi > overlapLo
        ? (overlapLo + overlapHi) / 2
        : (from.crossLo + from.crossHi + to.crossLo + to.crossHi) / 4,
    fromElementId: from.id,
    toElementId: to.id,
    isMatched: true,
  };
}

function gapGuides(axis: Axis, box: Span, targets: Span[]): SpacingGuide[] {
  const { before, after, gaps } = rowOf(box, targets, 0);
  const guides: SpacingGuide[] = [];
  const beforeGap = before ? box.lo - before.hi : 0;
  const afterGap = after ? after.lo - box.hi : 0;

  // The gap nearest to the box that has the same size, to compare against.
  const sameAs = (size: number) =>
    gaps
      .filter((gap) => meets(gap.size, size))
      .sort(
        (a, b) =>
          Math.abs((a.from.hi + a.to.lo) / 2 - box.mid) -
          Math.abs((b.from.hi + b.to.lo) / 2 - box.mid),
      )[0];

  const references = new Set<Gap>();
  let showBefore = false;
  let showAfter = false;
  if (before && beforeGap > 0) {
    const reference = sameAs(beforeGap);
    if (reference) {
      references.add(reference);
      showBefore = true;
    }
  }
  if (after && afterGap > 0) {
    const reference = sameAs(afterGap);
    if (reference) {
      references.add(reference);
      showAfter = true;
    }
  }
  // In the middle of its two neighbours, as near as whole units allow.
  if (
    before &&
    after &&
    beforeGap > 0 &&
    afterGap > 0 &&
    Math.abs(beforeGap - afterGap) <= 1 + EPSILON
  ) {
    showBefore = true;
    showAfter = true;
  }

  for (const reference of references) {
    guides.push(gapGuide(axis, reference.from, reference.to));
  }
  if (showBefore && before) guides.push(gapGuide(axis, before, box));
  if (showAfter && after) guides.push(gapGuide(axis, box, after));
  return guides;
}

export interface MoveSnap {
  left: number;
  top: number;
  guides: GuideLine[];
  spacingGuides: SpacingGuide[];
}

/**
 * Where a moved box lands. `box` is where the pointer puts it; an axis that
 * is not `free` keeps its place and takes no part.
 */
export function snapMove(
  box: ElementBounds,
  targets: ElementBounds[],
  options: SnapOptions,
  free: { x: boolean; y: boolean } = { x: true, y: true },
): MoveSnap {
  const others = targets.filter((target) => target.id !== box.id);
  const acrossX = others.map((target) => spanOf(target, 'x'));
  const acrossY = others.map((target) => spanOf(target, 'y'));

  const left = free.x
    ? settleMove(spanOf(box, 'x'), acrossX, CANVAS_CENTER_X, options)
    : box.left;
  const top = free.y
    ? settleMove(spanOf(box, 'y'), acrossY, CANVAS_CENTER_Y, options)
    : box.top;

  const guides: GuideLine[] = [];
  const spacingGuides: SpacingGuide[] = [];
  if (!options.suspended) {
    const landed = calculateBounds(left, top, box.width, box.height, box.id);
    const axes: [Axis, boolean, Span[], number][] = [
      ['x', free.x, acrossX, CANVAS_CENTER_X],
      ['y', free.y, acrossY, CANVAS_CENTER_Y],
    ];
    for (const [axis, isFree, spans, canvasCenter] of axes) {
      if (!isFree) continue;
      const landedSpan = spanOf(landed, axis);
      if (options.alignment) {
        guides.push(...moveLines(axis, landedSpan, spans, canvasCenter));
      }
      if (options.spacing) {
        spacingGuides.push(...gapGuides(axis, landedSpan, spans));
      }
    }
  }
  return { left, top, guides, spacingGuides };
}

// Resizing --------------------------------------------------------------------

/** Which side of an axis the pointer pulls: its low side, none, its high. */
export type PulledSide = -1 | 0 | 1;

export interface Edges {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

function settleResize(
  lo: number,
  hi: number,
  side: PulledSide,
  targets: Span[],
  options: SnapOptions,
  minSize: number,
): [number, number] {
  if (side === 0) return [lo, hi];
  const anchor = side === 1 ? lo : hi;
  let edge = side === 1 ? hi : lo;

  if (options.suspended) {
    edge = Math.round(edge);
  } else {
    const nearest = new Nearest();
    for (const target of targets) {
      if (options.alignment) {
        nearest.offer(target.lo - edge, options.reach);
        nearest.offer(target.hi - edge, options.reach);
      }
      if (options.sizeMatch) {
        nearest.offer(anchor + side * target.size - edge, options.sizeReach);
      }
    }
    edge =
      nearest.delta !== null
        ? Math.round(edge + nearest.delta)
        : toGrid(edge, options.grid);
  }

  // Only the pulled side ever moves; the other one is the anchor.
  return side === 1
    ? [lo, Math.max(edge, anchor + minSize)]
    : [Math.min(edge, anchor - minSize), hi];
}

function resizeLines(
  axis: Axis,
  box: Span,
  side: PulledSide,
  targets: Span[],
): GuideLine[] {
  const [low, , high] = lineNames[axis];
  const edge = side === 1 ? box.hi : box.lo;
  const name = side === 1 ? high : low;
  const lines = new Lines(axis);
  for (const target of targets) {
    if (meets(edge, target.lo)) lines.join(target.lo, name, box, target);
    if (meets(edge, target.hi)) lines.join(target.hi, name, box, target);
  }
  return lines.all();
}

function sameSizeGuide(
  dimension: 'width' | 'height',
  box: ElementBounds,
  targets: ElementBounds[],
): SizeMatchGuide | null {
  const size = dimension === 'width' ? box.width : box.height;
  const match = targets
    .filter((target) =>
      meets(dimension === 'width' ? target.width : target.height, size),
    )
    .sort(
      (a, b) =>
        Math.hypot(a.centerX - box.centerX, a.centerY - box.centerY) -
        Math.hypot(b.centerX - box.centerX, b.centerY - box.centerY),
    )[0];
  if (!match) return null;
  return {
    type: 'size-match',
    dimension,
    value: dimension === 'width' ? match.width : match.height,
    position:
      dimension === 'width'
        ? { x: match.centerX, y: match.top - 15 }
        : { x: match.right + 15, y: match.centerY },
    matchedElementId: match.id,
    matchedElementBounds: {
      left: match.left,
      top: match.top,
      width: match.width,
      height: match.height,
    },
  };
}

export interface ResizeSnap extends Edges {
  guides: GuideLine[];
  sizeMatchGuides: SizeMatchGuide[];
}

/**
 * Where a pulled edge lands. `box` is where the pointer puts the edges;
 * `pulled` says which side of each axis the pointer holds. The other sides
 * come back exactly as given.
 */
export function snapResize(
  box: Edges & { id: string },
  pulled: { x: PulledSide; y: PulledSide },
  targets: ElementBounds[],
  options: SnapOptions,
  minSize: number | { x: number; y: number } = 1,
): ResizeSnap {
  const least =
    typeof minSize === 'number' ? { x: minSize, y: minSize } : minSize;
  const others = targets.filter((target) => target.id !== box.id);
  const acrossX = others.map((target) => spanOf(target, 'x'));
  const acrossY = others.map((target) => spanOf(target, 'y'));

  const [left, right] = settleResize(
    box.left,
    box.right,
    pulled.x,
    acrossX,
    options,
    least.x,
  );
  const [top, bottom] = settleResize(
    box.top,
    box.bottom,
    pulled.y,
    acrossY,
    options,
    least.y,
  );

  const guides: GuideLine[] = [];
  const sizeMatchGuides: SizeMatchGuide[] = [];
  if (!options.suspended) {
    const landed = calculateBounds(
      left,
      top,
      right - left,
      bottom - top,
      box.id,
    );
    const axes: [Axis, PulledSide, Span[], 'width' | 'height'][] = [
      ['x', pulled.x, acrossX, 'width'],
      ['y', pulled.y, acrossY, 'height'],
    ];
    for (const [axis, side, spans, dimension] of axes) {
      if (side === 0) continue;
      if (options.alignment) {
        guides.push(...resizeLines(axis, spanOf(landed, axis), side, spans));
      }
      if (options.sizeMatch) {
        const guide = sameSizeGuide(dimension, landed, others);
        if (guide) sizeMatchGuides.push(guide);
      }
    }
  }
  return { left, top, right, bottom, guides, sizeMatchGuides };
}
