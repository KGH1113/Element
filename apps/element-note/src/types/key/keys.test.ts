import { describe, it, expect } from 'vitest';
import { keyPositionSchema, normalizeCounterSettings } from './keys';
import { createDefaultKeyPosition } from '@src/renderer/editor/model/keys';

describe('keyPositionSchema 시각 px 필드 소수 허용', () => {
  it('v2 안정 신원과 추가 스타일을 보존하면서 v1 기본값을 유지한다', () => {
    const parsed = keyPositionSchema.parse({
      ...createDefaultKeyPosition(),
      id: '11111111-1111-4111-8111-111111111111',
      rotation: 12,
      backgroundGradient: { type: 'linear', angle: 90 },
    });
    expect(parsed.id).toBe('11111111-1111-4111-8111-111111111111');
    expect(parsed.rotation).toBe(12);
    expect(parsed.backgroundGradient).toEqual({ type: 'linear', angle: 90 });
    expect(parsed.counter.fontSize).toBe(16);
    expect(parsed.counter.fontWeight).toBe(700);
  });

  it('v1 카운터 정규화 중 v2 전용 속성을 지우지 않는다', () => {
    const normalized = normalizeCounterSettings({
      fontSize: 18,
      fontBold: true,
      fillIdleGradient: { type: 'linear', angle: 45 },
    });
    expect(normalized).toMatchObject({
      fontSize: 18,
      fontBold: true,
      fillIdleGradient: { type: 'linear', angle: 45 },
    });
  });
  it('noteWidth 소수 허용', () => {
    expect(keyPositionSchema.shape.noteWidth.parse(100.5)).toBe(100.5);
    expect(keyPositionSchema.shape.noteWidth.parse(100)).toBe(100);
  });

  it('noteBorderRadius 소수 허용', () => {
    expect(keyPositionSchema.shape.noteBorderRadius.parse(8.5)).toBe(8.5);
  });

  it('noteGlowSize 소수 허용', () => {
    expect(keyPositionSchema.shape.noteGlowSize.parse(20.5)).toBe(20.5);
  });

  it('noteBorderWidth 소수 허용', () => {
    expect(keyPositionSchema.shape.noteBorderWidth.parse(2.5)).toBe(2.5);
  });

  // 정수 유지 필드는 여전히 소수를 거부해야 한다
  it('noteOpacity는 소수 거부(정수 유지)', () => {
    expect(() => keyPositionSchema.shape.noteOpacity.parse(80.5)).toThrow();
  });
});
