import { describe, expect, it } from 'vitest';
import { computeKeyElementStyles } from './useKeyElementStyles';

const basePosition = {
  dx: 0,
  dy: 0,
  width: 60,
  height: 60,
  backgroundColor: 'rgba(46, 46, 47, 0.9)',
  borderColor: 'rgba(113, 113, 113, 0.9)',
  borderWidth: 3,
  fontColor: '#ffffff',
};

describe('computeKeyElementStyles active feedback', () => {
  it('uses the v1 active defaults when active colors are not stored', () => {
    const inactive = computeKeyElementStyles({
      position: basePosition,
      active: false,
      label: 'A',
    });
    const active = computeKeyElementStyles({
      position: basePosition,
      active: true,
      label: 'A',
    });

    expect(active.keyStyle.backgroundColor).toBe(
      'var(--key-bg, rgba(121, 121, 121, 0.9))',
    );
    expect(active.keyStyle.border).toBe(
      'var(--key-border, 3px solid rgba(255, 255, 255, 0.9))',
    );
    expect(active.keyStyle.backgroundColor).not.toBe(
      inactive.keyStyle.backgroundColor,
    );
    expect(active.keyStyle.border).not.toBe(inactive.keyStyle.border);
  });

  it('preserves explicitly configured active colors', () => {
    const active = computeKeyElementStyles({
      position: {
        ...basePosition,
        activeBackgroundColor: '#123456',
        activeBorderColor: '#abcdef',
        activeFontColor: '#fedcba',
      },
      active: true,
      label: 'A',
    });

    expect(active.keyStyle.backgroundColor).toBe('var(--key-bg, #123456)');
    expect(active.keyStyle.border).toBe('var(--key-border, 3px solid #abcdef)');
    expect(active.keyStyle.color).toBe('var(--key-text-color, #fedcba)');
  });
});
