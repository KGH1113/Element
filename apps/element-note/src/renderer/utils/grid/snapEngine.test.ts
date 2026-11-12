import { describe, expect, it } from 'vitest';
import { calculateBounds, type ElementBounds } from './smartGuides';
import { snapMove, snapOptionsFor, snapResize } from './snapEngine';

const box = (
  id: string,
  left: number,
  top: number,
  width = 60,
  height = 60,
): ElementBounds => calculateBounds(left, top, width, height, id);

const at1 = snapOptionsFor({ zoom: 1, grid: 5 });

describe('moving', () => {
  it('lands on the grid when no other element is near', () => {
    const moved = snapMove(box('me', 203.4, 97.6), [], at1);
    expect([moved.left, moved.top]).toEqual([205, 100]);
    expect(moved.guides).toEqual([]);
  });

  it('uses one grid at every zoom', () => {
    for (const zoom of [0.5, 0.75, 1, 1.5, 2, 3]) {
      const options = snapOptionsFor({ zoom, grid: 5 });
      // 400 away from everything, so only the grid can decide.
      const moved = snapMove(box('me', 403.4, 397.6), [], options);
      expect([zoom, moved.left, moved.top]).toEqual([zoom, 405, 400]);
    }
  });

  it('goes to the nearest line, not the first one found', () => {
    const far = box('far', 100, 300); // left 100: 4 away
    const near = box('near', 97, 600); // left 97: 1 away
    const moved = snapMove(box('me', 96, 0), [far, near], at1);
    expect(moved.left).toBe(97);
  });

  it('reaches the same distance on screen at every zoom', () => {
    // An edge that is not on the grid, so only reaching it can land there.
    const other = box('other', 102, 300);
    for (const zoom of [0.5, 1, 2, 4]) {
      const options = snapOptionsFor({ zoom, grid: 5 });
      const inside = snapMove(box('me', 102 + 4 / zoom, 0), [other], options);
      const outside = snapMove(box('me', 102 + 7 / zoom, 0), [other], options);
      expect([zoom, inside.left]).toEqual([zoom, 102]);
      expect([zoom, outside.left === 102]).toEqual([zoom, false]);
    }
  });

  it('keeps every grid place next to an element reachable', () => {
    const other = box('other', 100, 300);
    const lefts = new Set<number>();
    for (let raw = 80; raw <= 120; raw += 0.25) {
      lefts.add(snapMove(box('me', raw, 0), [other], at1).left);
    }
    for (const place of [80, 85, 90, 95, 100, 105, 110, 115, 120]) {
      expect(lefts.has(place)).toBe(true);
    }
  });

  it('puts an element side by side with another, with no gap', () => {
    const other = box('other', 100, 0);
    const moved = snapMove(box('me', 162, 0), [other], at1);
    expect(moved.left).toBe(160);
    expect(
      moved.guides.some((g) => g.type === 'vertical' && g.position === 160),
    ).toBe(true);
  });

  it('continues the gap of a row', () => {
    // A [gap 8] B, and the dragged key comes after B.
    const a = box('a', 0, 0);
    const b = box('b', 68, 0);
    const moved = snapMove(box('me', 134, 2), [a, b], at1);
    expect(moved.left).toBe(136);
    expect(moved.top).toBe(0);
    const gaps = moved.spacingGuides.filter(
      (g) => g.direction === 'horizontal',
    );
    expect(gaps.map((g) => g.value)).toEqual([8, 8]);
    expect(gaps[1]).toMatchObject({ startPos: 128, endPos: 136 });
  });

  it('centres an element between its two neighbours', () => {
    const a = box('a', 0, 0);
    const b = box('b', 200, 0);
    // Room is 140, the key is 60 wide: 40 on each side.
    const moved = snapMove(box('me', 103, 0), [a, b], at1);
    expect(moved.left).toBe(100);
    expect(moved.spacingGuides.map((g) => g.value)).toEqual([40, 40]);
  });

  it('does not let an equal gap win over a nearer line', () => {
    const a = box('a', 0, 0);
    const b = box('b', 68, 0);
    // Left edge of `c` is at 133: 1 away. The gap place is 136: 2 away.
    const c = box('c', 133, 200);
    const moved = snapMove(box('me', 134, 0), [a, b, c], at1);
    expect(moved.left).toBe(133);
    expect(
      moved.spacingGuides.filter((g) => g.direction === 'horizontal'),
    ).toEqual([]);
  });

  it('draws a guide only for what is true of where it landed', () => {
    const other = box('other', 100, 300);
    // 9 away: out of reach, so the grid decides and nothing lines up.
    const moved = snapMove(box('me', 109, 0), [other], at1);
    expect(moved.left).toBe(110);
    expect(moved.guides.filter((g) => g.type === 'vertical')).toEqual([]);
  });

  it('gives each guide the length of what it joins', () => {
    const other = box('other', 100, 300);
    const moved = snapMove(box('me', 101, 0), [other], at1);
    const guide = moved.guides.find(
      (g) => g.type === 'vertical' && g.position === 100,
    )!;
    expect(guide.start).toBeLessThan(0);
    expect(guide.end).toBeGreaterThan(360);
  });

  it('keeps a locked axis exactly where it was', () => {
    const other = box('other', 100, 3);
    const moved = snapMove(box('me', 203.4, 2), [other], at1, {
      x: true,
      y: false,
    });
    expect(moved.top).toBe(2);
    expect(moved.guides.filter((g) => g.type === 'horizontal')).toEqual([]);
  });

  it('only rounds while snapping is suspended', () => {
    const other = box('other', 100, 0);
    const moved = snapMove(
      box('me', 101.4, 2.6),
      [other],
      snapOptionsFor({ zoom: 1, grid: 5, suspended: true }),
    );
    expect([moved.left, moved.top]).toEqual([101, 3]);
    expect(moved.guides).toEqual([]);
    expect(moved.spacingGuides).toEqual([]);
  });

  it('still lines up when equal gaps are turned off, and the other way round', () => {
    const a = box('a', 0, 0);
    const b = box('b', 68, 0);
    const noGaps = snapMove(
      box('me', 134, 2),
      [a, b],
      snapOptionsFor({ zoom: 1, grid: 5, spacing: false }),
    );
    expect([noGaps.left, noGaps.top]).toEqual([135, 0]);
    const noLines = snapMove(
      box('me', 134, 2),
      [a, b],
      snapOptionsFor({ zoom: 1, grid: 5, alignment: false }),
    );
    expect([noLines.left, noLines.top]).toEqual([136, 0]);
  });

  it('always returns whole units', () => {
    const odd = box('odd', 100, 0, 45, 45); // its centre is at 122.5
    const moved = snapMove(box('me', 93.1, 300.2), [odd], at1);
    expect(Number.isInteger(moved.left)).toBe(true);
    expect(Number.isInteger(moved.top)).toBe(true);
  });
});

describe('resizing', () => {
  const edges = (
    left: number,
    top: number,
    right: number,
    bottom: number,
    id = 'me',
  ) => ({ id, left, top, right, bottom });

  it('moves only the pulled edge', () => {
    const other = box('other', 300, 300, 200, 200);
    const result = snapResize(
      edges(12, 7, 71.6, 67),
      { x: 1, y: 0 },
      [other],
      at1,
    );
    expect(result).toMatchObject({ left: 12, top: 7, right: 70, bottom: 67 });
  });

  it('does not pull the edge because the other edge or the centre lines up', () => {
    // The fixed left edge is already in line with `other`; so would the
    // centre be. Neither may move the right edge.
    const other = box('other', 100, 300, 200, 60);
    const result = snapResize(
      edges(100, 0, 163, 60),
      { x: 1, y: 0 },
      [other],
      at1,
    );
    expect(result.right).toBe(165);
    expect(result.left).toBe(100);
  });

  it('pulls the dragged edge to another element’s edge', () => {
    const other = box('other', 300, 300, 100, 60); // right edge 400
    const result = snapResize(
      edges(250, 0, 397, 60),
      { x: 1, y: 0 },
      [other],
      at1,
    );
    expect(result.right).toBe(400);
    expect(result.guides).toEqual([
      expect.objectContaining({ type: 'vertical', position: 400 }),
    ]);
  });

  it('matches another element’s size on the pulled axis only', () => {
    const other = box('other', 300, 300, 72, 91);
    const result = snapResize(
      edges(0, 0, 70, 90),
      { x: 1, y: 0 },
      [other],
      at1,
    );
    expect(result.right).toBe(72);
    // The height was 1 away from 91 but was not being dragged.
    expect(result.bottom).toBe(90);
    expect(result.sizeMatchGuides.map((g) => g.dimension)).toEqual(['width']);
  });

  it('puts the pulled edge on the grid, whichever side it is', () => {
    const fromRight = snapResize(edges(12, 0, 73, 60), { x: 1, y: 0 }, [], at1);
    expect([fromRight.left, fromRight.right]).toEqual([12, 75]);
    const fromLeft = snapResize(
      edges(12.4, 0, 73, 60),
      { x: -1, y: 0 },
      [],
      at1,
    );
    expect([fromLeft.left, fromLeft.right]).toEqual([10, 73]);
  });

  it('never makes the element smaller than the smallest size', () => {
    const result = snapResize(
      edges(100, 0, 103, 60),
      { x: 1, y: 0 },
      [],
      at1,
      10,
    );
    expect(result.right).toBe(110);
    const fromTop = snapResize(
      edges(0, 58, 60, 60),
      { x: 0, y: -1 },
      [],
      at1,
      10,
    );
    expect(fromTop.top).toBe(50);
  });

  it('pulls a corner on both axes independently', () => {
    const other = box('other', 300, 300, 100, 100);
    const result = snapResize(
      edges(0, 0, 298, 143),
      { x: 1, y: 1 },
      [other],
      at1,
    );
    expect([result.right, result.bottom]).toEqual([300, 145]);
  });
});
