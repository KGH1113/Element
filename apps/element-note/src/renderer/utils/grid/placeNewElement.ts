/**
 * Where a newly added element goes: in the part of the canvas that can be
 * seen, as near its middle as possible, and never on top of another element
 * or under something that lies over the canvas. So it can always be seen and
 * pressed right after it is added.
 */

export interface Box {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

const intersects = (a: Box, b: Box) =>
  a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;

const grow = (box: Box, by: number): Box => ({
  left: box.left - by,
  top: box.top - by,
  right: box.right + by,
  bottom: box.bottom + by,
});

export function placeNewElement(input: {
  size: { width: number; height: number };
  /** The part of the canvas that can be seen, in canvas units. */
  view: Box;
  /** Elements already on the canvas. */
  occupied: Box[];
  /** Parts of the view that something lies over, such as the minimap. */
  covered?: Box[];
  grid: number;
  /** Room kept between the new element and anything else. */
  gap?: number;
}): { x: number; y: number } {
  const { size, view, occupied, covered = [] } = input;
  const grid = Math.max(1, Math.round(input.grid));
  const gap = input.gap ?? 10;
  const toGrid = (value: number) => Math.round(value / grid) * grid;

  const centre = {
    x: toGrid((view.left + view.right) / 2 - size.width / 2),
    y: toGrid((view.top + view.bottom) / 2 - size.height / 2),
  };

  const blocked = [...occupied.map((box) => grow(box, gap)), ...covered];
  const fits = (x: number, y: number) => {
    const box = {
      left: x,
      top: y,
      right: x + size.width,
      bottom: y + size.height,
    };
    return (
      box.left >= view.left + gap &&
      box.top >= view.top + gap &&
      box.right <= view.right - gap &&
      box.bottom <= view.bottom - gap &&
      !blocked.some((other) => intersects(box, other))
    );
  };

  // Rings around the middle, nearest first. Steps are whole grid cells.
  const step = grid * Math.max(1, Math.ceil(20 / grid));
  const reach = Math.ceil(
    Math.max(view.right - view.left, view.bottom - view.top) / 2 / step,
  );
  for (let ring = 0; ring <= reach; ring += 1) {
    let best: { x: number; y: number; far: number } | null = null;
    for (let i = -ring; i <= ring; i += 1) {
      for (let j = -ring; j <= ring; j += 1) {
        if (Math.max(Math.abs(i), Math.abs(j)) !== ring) continue;
        const x = centre.x + i * step;
        const y = centre.y + j * step;
        if (!fits(x, y)) continue;
        const far = Math.hypot(i, j);
        if (!best || far < best.far) best = { x, y, far };
      }
    }
    if (best) return { x: best.x, y: best.y };
  }

  // Nowhere is free (a tiny view or a full canvas): the middle it is.
  return centre;
}
