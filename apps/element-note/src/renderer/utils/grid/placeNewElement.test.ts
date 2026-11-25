import { describe, expect, it } from 'vitest';
import { placeNewElement, type Box } from './placeNewElement';

const view: Box = { left: 0, top: 0, right: 800, bottom: 600 };
const size = { width: 60, height: 60 };
const boxAt = (x: number, y: number, w = 60, h = 60): Box => ({
  left: x,
  top: y,
  right: x + w,
  bottom: y + h,
});
const overlaps = (a: Box, b: Box) =>
  a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;

describe('where a new element goes', () => {
  it('is the middle of what can be seen when that is free', () => {
    expect(placeNewElement({ size, view, occupied: [], grid: 5 })).toEqual({
      x: 370,
      y: 270,
    });
  });

  it('follows the view when it is panned or zoomed', () => {
    const panned: Box = { left: 1000, top: -400, right: 1400, bottom: -100 };
    const place = placeNewElement({
      size,
      view: panned,
      occupied: [],
      grid: 5,
    });
    expect(place).toEqual({ x: 1170, y: -280 });
  });

  it('never lands on an element, and keeps a little room from it', () => {
    const occupied = [boxAt(370, 270)];
    const place = placeNewElement({ size, view, occupied, grid: 5 });
    const placed = boxAt(place.x, place.y);
    expect(overlaps(placed, occupied[0])).toBe(false);
    // Near the middle, not somewhere far off.
    expect(Math.hypot(place.x - 370, place.y - 270)).toBeLessThan(120);
    const roomX = Math.max(
      occupied[0].left - placed.right,
      placed.left - occupied[0].right,
    );
    const roomY = Math.max(
      occupied[0].top - placed.bottom,
      placed.top - occupied[0].bottom,
    );
    expect(Math.max(roomX, roomY)).toBeGreaterThanOrEqual(10);
  });

  it('does not stack elements that are added one after another', () => {
    const occupied: Box[] = [];
    for (let count = 0; count < 12; count += 1) {
      const place = placeNewElement({ size, view, occupied, grid: 5 });
      const placed = boxAt(place.x, place.y);
      expect(occupied.some((other) => overlaps(placed, other))).toBe(false);
      occupied.push(placed);
    }
  });

  it('stays inside what can be seen and out from under the minimap', () => {
    // A layout fills the middle; the minimap covers the bottom left.
    const occupied = [boxAt(100, 150, 600, 300)];
    const covered = [{ left: 0, top: 380, right: 240, bottom: 600 }];
    const place = placeNewElement({ size, view, occupied, covered, grid: 5 });
    const placed = boxAt(place.x, place.y);
    expect(placed.left).toBeGreaterThanOrEqual(view.left);
    expect(placed.top).toBeGreaterThanOrEqual(view.top);
    expect(placed.right).toBeLessThanOrEqual(view.right);
    expect(placed.bottom).toBeLessThanOrEqual(view.bottom);
    expect(overlaps(placed, occupied[0])).toBe(false);
    expect(overlaps(placed, covered[0])).toBe(false);
  });

  it('lands on the grid', () => {
    const place = placeNewElement({
      size: { width: 61, height: 47 },
      view: { left: 3, top: 7, right: 803, bottom: 607 },
      occupied: [],
      grid: 5,
    });
    expect(place.x % 5).toBe(0);
    expect(place.y % 5).toBe(0);
  });

  it('falls back to the middle when nothing is free', () => {
    const full = [boxAt(-1000, -1000, 4000, 4000)];
    expect(placeNewElement({ size, view, occupied: full, grid: 5 })).toEqual({
      x: 370,
      y: 270,
    });
  });
});
