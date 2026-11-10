import { describe, expect, it } from 'vitest';
import {
  DEFAULT_NOTE_SETTINGS,
  FALLBACK_POSITION,
} from '@constants/overlayDefaults';
import { createNoteBuffer } from '@stores/signals/noteBuffer';
import type { KeyPosition } from '@src/types/key/keys';
import { computeLayout } from './useLayoutComputation';

const layout = (positions: KeyPosition[]) =>
  computeLayout({
    currentKeys: positions.map((_, index) => `key-${index}`),
    currentPositions: positions,
    currentStatPositions: [],
    currentGraphPositions: [],
    currentKnobPositions: [],
    trackHeight: 150,
    noteSettings: { ...DEFAULT_NOTE_SETTINGS, fadePosition: 'auto' },
  });

describe('v2 note paint through the Element layout boundary', () => {
  it('retains the v1 unrotated hitline, dimensions and glow defaults', () => {
    const result = layout([{ ...FALLBACK_POSITION, dx: 80, dy: 120 }]);
    const track = result.webglTracks[0]!;
    expect(track.position.dx).toBe(30);
    expect(track.position.dy).toBe(180);
    expect(track.direction).toEqual({ x: 0, y: -1 });
    expect(track.width).toBe(60);
    expect(track.height).toBe(150);
    expect(track.flowSpeed).toBe(180);
    expect(track.noteGlowSize).toBe(20);
  });

  it('passes multi-stop body and border paint and preserves implicit glow inheritance', () => {
    const gradient = {
      angle: 45,
      stops: [
        { color: '#ff000080', pos: 0 },
        { color: '#00ff0080', pos: 0.3 },
        { color: '#0000ff', pos: 1 },
      ],
    };
    const result = layout([
      {
        ...FALLBACK_POSITION,
        noteGradient: gradient,
        noteGlowColor: undefined,
        noteGlowEnabled: true,
        noteBorderGradient: gradient,
        noteBorderWidth: 2,
      },
    ]);
    const track = result.webglTracks[0]!;
    expect(track.noteGradient).toEqual(gradient);
    expect(track.noteBorderGradient).toEqual(gradient);
    expect(track.noteGlowColor).toBeUndefined();
    const buffer = createNoteBuffer();
    buffer.updateTrackLayouts([track]);
    buffer.allocate('key-0', 'note', 100);
    expect(buffer.noteBodyPaint[0]).toBeGreaterThanOrEqual(0);
    expect(buffer.noteGlowPaint[0]).toBeGreaterThanOrEqual(0);
    expect(buffer.noteGlowPaint[0]).not.toBe(buffer.noteBodyPaint[0]);
    expect(buffer.noteGlowPaint[3]).toBe(0);
    expect(buffer.noteBorderGradientInfo[0]).toBeGreaterThanOrEqual(0);
  });

  it('aligns rotated tracks sharing a flow direction without changing opted-out tracks', () => {
    const positions = [0, 100].map((dx) => ({
      ...FALLBACK_POSITION,
      dx,
      rotation: 90,
    }));
    const aligned = layout(positions).webglTracks;
    expect(aligned[0]!.position.dx).toBeCloseTo(aligned[1]!.position.dx, 5);
    expect(aligned[0]!.direction.x).toBeCloseTo(1, 5);
    const unaligned = layout(
      positions.map((position) => ({
        ...position,
        noteAutoYCorrection: false,
      })),
    ).webglTracks;
    expect(unaligned[1]!.position.dx - unaligned[0]!.position.dx).toBeCloseTo(
      100,
      5,
    );
  });
});
