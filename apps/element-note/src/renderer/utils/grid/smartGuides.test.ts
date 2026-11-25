import { describe, expect, it } from 'vitest';
import { calculateBounds, calculateRotatedBounds } from './smartGuides';

describe('the box an element is seen in', () => {
  it('is the element itself when it is not turned', () => {
    expect(calculateRotatedBounds(10, 20, 100, 40, 0, 'a')).toEqual(
      calculateBounds(10, 20, 100, 40, 'a'),
    );
    expect(calculateRotatedBounds(10, 20, 100, 40, undefined, 'a')).toEqual(
      calculateBounds(10, 20, 100, 40, 'a'),
    );
  });

  it('stands upright around a quarter turn, about the same centre', () => {
    const seen = calculateRotatedBounds(0, 0, 100, 40, 90, 'a');
    expect(seen.left).toBeCloseTo(30);
    expect(seen.top).toBeCloseTo(-30);
    expect(seen.width).toBeCloseTo(40);
    expect(seen.height).toBeCloseTo(100);
    expect(seen.centerX).toBeCloseTo(50);
    expect(seen.centerY).toBeCloseTo(20);
  });

  it('grows to hold a slanted element', () => {
    const seen = calculateRotatedBounds(0, 0, 100, 100, 45, 'a');
    expect(seen.width).toBeCloseTo(Math.SQRT2 * 100);
    expect(seen.centerX).toBeCloseTo(50);
  });
});
