import { describe, expect, it } from 'vitest';
import { moveTabId, moveTabIdByOffset } from './tabOrder';

describe('tab order', () => {
  it('moves a dragged tab in display order', () => {
    expect(moveTabId(['c', 'b', 'a'], 'a', 'c')).toEqual(['a', 'c', 'b']);
  });

  it('moves by keyboard and clamps at the edges', () => {
    expect(moveTabIdByOffset(['c', 'b', 'a'], 'b', -1)).toEqual([
      'b',
      'c',
      'a',
    ]);
    expect(moveTabIdByOffset(['c', 'b', 'a'], 'c', -1)).toEqual([
      'c',
      'b',
      'a',
    ]);
  });
});
