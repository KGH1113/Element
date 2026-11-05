import { describe, expect, it } from 'vitest';
import {
  applySpriteImage,
  createSpriteDraft,
  createSpritePose,
  normalizeSpriteDraft,
  validateSpriteDraft,
} from './spriteEditorDraft';

describe('sprite editor draft', () => {
  it('starts a valid sprite at the requested scene position', () => {
    const draft = createSpriteDraft(null, { dx: 120, dy: -48 });
    expect(validateSpriteDraft(draft)).toBeNull();
    expect(draft).toMatchObject({ dx: 120, dy: -48, poses: [] });
  });

  it('keeps unknown extension fields and original state isolated while editing', () => {
    const original = {
      ...createSpriteDraft(null),
      pluginData: { source: 'custom' },
      poses: [{ ...createSpritePose(), triggers: ['key-b', 'key-a', 'key-b'] }],
    };
    const draft = createSpriteDraft(original);
    draft.poses[0].transform.x = 90;
    const normalized = normalizeSpriteDraft(draft);
    expect(normalized).toMatchObject({ pluginData: { source: 'custom' } });
    expect(normalized.poses[0].triggers).toEqual(['key-a', 'key-b']);
    expect(original.poses[0].transform.x).toBe(0);
    expect(original.poses[0].triggers).toEqual(['key-b', 'key-a', 'key-b']);
  });

  it('prevents empty and duplicate trigger combinations', () => {
    const draft = createSpriteDraft(null);
    draft.poses = [createSpritePose()];
    expect(validateSpriteDraft(draft)).toBe('emptyTriggers');
    draft.poses = [
      { ...createSpritePose(), triggers: ['key-a', 'key-b'] },
      { ...createSpritePose(), triggers: ['key-b', 'key-a'] },
    ];
    expect(validateSpriteDraft(draft)).toBe('duplicateTriggers');
  });

  it('rejects values that cannot be committed to the v2 schema', () => {
    const draft = createSpriteDraft(null);
    expect(validateSpriteDraft({ ...draft, width: Number.NaN })).toBe('range');
    expect(validateSpriteDraft({ ...draft, pressDurationMs: 0 })).toBe('range');
    expect(validateSpriteDraft({ ...draft, transitionEasing: ' ' })).toBe(
      'easing',
    );
  });

  it('fits a selected image to its aspect ratio while retaining the scene pivot', () => {
    const draft = createSpriteDraft(null, { dx: 10, dy: 20 });
    const result = applySpriteImage(
      draft,
      '/sprite.png',
      { width: 400, height: 200 },
      null,
    );
    expect(result).toMatchObject({
      dx: 10,
      dy: 70,
      width: 200,
      height: 100,
      baseImage: '/sprite.png',
      referenceNaturalSize: { source: '/sprite.png', width: 400, height: 200 },
    });
    expect(draft.baseImage).toBeNull();
  });

  it('binds a pose image to its natural size without changing the base geometry', () => {
    const base = applySpriteImage(
      createSpriteDraft(null),
      '/base.png',
      { width: 400, height: 200 },
      null,
    );
    const pose = createSpritePose();
    base.poses = [pose];
    const result = applySpriteImage(
      base,
      '/pose.png',
      { width: 600, height: 100 },
      pose.poseId,
    );
    expect(result.referenceNaturalSize).toEqual(base.referenceNaturalSize);
    expect(result.width).toBe(base.width);
    expect(result.poses[0]).toMatchObject({
      imageOverride: '/pose.png',
      imageOverrideMetrics: { source: '/pose.png', width: 600, height: 100 },
    });
    expect(pose.imageOverride).toBeNull();
  });

  it('anchors the first pose-only image to a source-free reference size', () => {
    const draft = createSpriteDraft(null);
    const pose = createSpritePose();
    draft.poses = [pose];
    const result = applySpriteImage(
      draft,
      '/pose.png',
      { width: 640, height: 480 },
      pose.poseId,
    );
    expect(result.referenceNaturalSize).toEqual({
      source: null,
      width: 640,
      height: 480,
    });
  });
});
