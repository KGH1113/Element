import { describe, expect, it } from 'vitest';
import { makeSpritePose, makeSpritePosition } from './spriteFixtures';
import {
  keepUnsavedPoses,
  poseNeedsKey,
  savableSprite,
  savableSprites,
} from './unsavedPoses';

const bound = makeSpritePose({ poseId: 'bound', triggers: ['b', 'a', 'a'] });
const waiting = makeSpritePose({ poseId: 'waiting', triggers: [] });

describe('poses that still need a key', () => {
  it('are left out of what is saved, and saved keys are put in one order', () => {
    const sprite = makeSpritePosition({ poses: [waiting, bound] });
    const saved = savableSprite(sprite);
    expect(saved.poses.map((pose) => pose.poseId)).toEqual(['bound']);
    expect(saved.poses[0].triggers).toEqual(['a', 'b']);
    // The sprite on screen keeps the pose that waits.
    expect(sprite.poses).toHaveLength(2);
    expect(poseNeedsKey(sprite.poses[0])).toBe(true);
  });

  it('saves every tab, each without its waiting poses', () => {
    const saved = savableSprites({
      hand: [makeSpritePosition({ id: 'a', poses: [waiting] })],
      foot: [makeSpritePosition({ id: 'b', poses: [bound] })],
    });
    expect(saved.hand[0].poses).toEqual([]);
    expect(saved.foot[0].poses).toHaveLength(1);
  });

  it('takes what the backend reports as it is when nothing waits', () => {
    const local = { hand: [makeSpritePosition({ id: 'a', poses: [bound] })] };
    const incoming = {
      hand: [makeSpritePosition({ id: 'a', dx: 9, poses: [bound] })],
    };
    expect(keepUnsavedPoses(local, incoming)).toBe(incoming);
  });

  it('puts a waiting pose back where it was in what the backend reports', () => {
    const other = makeSpritePosition({ id: 'other' });
    const local = {
      hand: [makeSpritePosition({ id: 'a', poses: [waiting, bound] }), other],
    };
    const reported = makeSpritePosition({ id: 'a', dx: 40, poses: [bound] });
    const incoming = { hand: [reported, other], foot: [] };

    const kept = keepUnsavedPoses(local, incoming);
    expect(kept.hand[0].dx).toBe(40);
    expect(kept.hand[0].poses.map((pose) => pose.poseId)).toEqual([
      'waiting',
      'bound',
    ]);
    // Sprites and tabs without a waiting pose are the reported ones.
    expect(kept.hand[1]).toBe(other);
    expect(kept.foot).toBe(incoming.foot);
    expect(reported.poses).toHaveLength(1);
  });

  it('lets the backend win for a pose it has, and drops poses of a sprite that is gone', () => {
    const local = {
      hand: [
        makeSpritePosition({
          id: 'a',
          poses: [makeSpritePose({ poseId: 'p', triggers: [] })],
        }),
        makeSpritePosition({ id: 'gone', poses: [waiting] }),
      ],
    };
    const restored = makeSpritePose({ poseId: 'p', triggers: ['a'] });
    const incoming = {
      hand: [makeSpritePosition({ id: 'a', poses: [restored] })],
    };
    const kept = keepUnsavedPoses(local, incoming);
    expect(kept).toBe(incoming);
    expect(kept.hand).toHaveLength(1);
    expect(kept.hand[0].poses).toEqual([restored]);
  });
});
