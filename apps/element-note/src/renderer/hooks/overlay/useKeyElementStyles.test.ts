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

const gradient = {
  angle: 90,
  stops: [
    { color: '#ff000080', pos: 0 },
    { color: '#0000ff80', pos: 1 },
  ],
};

describe('computeKeyElementStyles active feedback', () => {
  it('leaves the v1 paint and image contract unchanged without v2 properties', () => {
    const style = computeKeyElementStyles({
      position: basePosition,
      active: false,
      label: 'A',
    });
    expect(style.borderRingStyle).toBeNull();
    expect(style.keyStyle.backgroundImage).toBeUndefined();
    expect(style.keyStyle.boxShadow).toBeUndefined();
    expect(style.keyStyle.transform).not.toContain('rotate');
    expect(style.textStyle.fontWeight).toBe(700);
  });

  it('renders explicit v2 gradients once without an opaque shadow color underneath', () => {
    const style = computeKeyElementStyles({
      position: {
        ...basePosition,
        backgroundGradient: gradient,
        borderGradient: gradient,
        fontGradient: gradient,
      },
      active: false,
      label: 'A',
    });
    expect(style.keyStyle.backgroundColor).toBe('transparent');
    expect(style.keyStyle.backgroundImage).toContain('linear-gradient(90deg');
    expect(style.borderRingStyle?.backgroundImage).toContain('linear-gradient');
    expect(style.keyStyle.padding).toBe('3px');
    expect(style.textStyle.backgroundClip).toBe('text');
    expect(style.textStyle.color).toBe('transparent');
  });

  it('lets an explicit active solid color replace an idle gradient atomically', () => {
    const style = computeKeyElementStyles({
      position: {
        ...basePosition,
        backgroundGradient: gradient,
        activeBackgroundColor: '#123456',
      },
      active: true,
      label: 'A',
    });
    expect(style.keyStyle.backgroundImage).toBeUndefined();
    expect(style.keyStyle.backgroundColor).toBe('var(--key-bg, #123456)');
  });

  it('applies stored rotation, explicit bold and state-specific shadows', () => {
    const style = computeKeyElementStyles({
      position: {
        ...basePosition,
        rotation: 45,
        fontWeight: 500,
        fontBold: true,
        activeShadow: {
          enabled: true,
          color: '#00000080',
          offsetX: 1,
          offsetY: 2,
          blur: 4,
        },
      },
      active: true,
      label: 'A',
    });
    expect(style.keyStyle.transform).toContain(' rotate(45deg)');
    expect(style.keyStyle.boxShadow).toBe('1px 2px 4px #00000080');
    expect(style.textStyle.fontWeight).toBe(800);
  });

  it('keeps the label under overlay images and uses the matching image transform', () => {
    const style = computeKeyElementStyles({
      position: {
        ...basePosition,
        imageMode: 'overlay',
        inactiveImage: 'data:image/png;base64,AA==',
        idleImageTransform: { offsetX: 4, offsetY: -2, rotation: 30, scale: 2 },
        activeImageTransform: {
          offsetX: 99,
          offsetY: 99,
          rotation: 0,
          scale: 1,
        },
      },
      active: true,
      label: 'A',
    });
    expect(style.hasCurrentImage).toBe(true);
    expect(style.imageReplaces).toBe(false);
    expect(style.keyStyle.overflow).toBe('visible');
    expect(style.keyStyle.contain).toBe('layout style');
    expect(style.imageStyle.position).toBe('absolute');
    expect(style.imageStyle.transform).toBe(
      'translate(4px, -2px) rotate(30deg) scale(2)',
    );
    expect(style.imageStyle.filter).toBe('none');
  });
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
