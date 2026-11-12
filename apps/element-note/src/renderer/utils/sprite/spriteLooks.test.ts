import { describe, expect, it } from 'vitest';
import { makeSpritePose, makeSpritePosition } from './spriteFixtures';
import {
  frameSpriteLooks,
  isSameSpriteLook,
  spriteFrameScale,
  spriteLookBounds,
  spritePoseLook,
  spriteRestLook,
} from './spriteLooks';

const sprite = makeSpritePosition({
  id: 'cat',
  width: 100,
  height: 60,
  baseImage: '/cat.png',
});

describe('the looks of an image', () => {
  it('rests in its own box', () => {
    const rest = spriteRestLook(sprite);
    expect(rest.src).toBe('/cat.png');
    expect(spriteLookBounds(rest)).toEqual({
      x: 0,
      y: 0,
      width: 100,
      height: 60,
    });
  });

  it('draws its own picture when a look has one, the image otherwise', () => {
    const own = makeSpritePose({ poseId: 'a', imageOverride: '/paw.png' });
    const plain = makeSpritePose({ poseId: 'b' });
    expect(spritePoseLook(sprite, own).src).toBe('/paw.png');
    expect(spritePoseLook(sprite, plain).src).toBe('/cat.png');
    expect(
      isSameSpriteLook(spritePoseLook(sprite, plain), spriteRestLook(sprite)),
    ).toBe(true);
    expect(
      isSameSpriteLook(spritePoseLook(sprite, own), spriteRestLook(sprite)),
    ).toBe(false);
  });

  it('covers the box it is moved, turned and sized to, around its centre', () => {
    const look = (transform: Partial<typeof sprite.idleTransform>) =>
      spriteLookBounds(
        spritePoseLook(
          sprite,
          makeSpritePose({
            transform: { x: 0, y: 0, rotation: 0, scale: 1, ...transform },
          }),
        ),
      );
    expect(look({ x: 30, y: -10 })).toEqual({
      x: 30,
      y: -10,
      width: 100,
      height: 60,
    });
    // Twice as big around the middle of the box.
    expect(look({ scale: 2 })).toEqual({
      x: -50,
      y: -30,
      width: 200,
      height: 120,
    });
    // A quarter turn swaps the sides and keeps the middle.
    const turned = look({ rotation: 90 });
    expect(turned.x).toBeCloseTo(20);
    expect(turned.y).toBeCloseTo(-20);
    expect(turned.width).toBeCloseTo(60);
    expect(turned.height).toBeCloseTo(100);
  });

  it('turns around the centre the image was given', () => {
    const corner = makeSpritePosition({
      id: 'cat',
      width: 100,
      height: 60,
      pivot: { x: 0, y: 0 },
    });
    const bounds = spriteLookBounds(
      spritePoseLook(
        corner,
        makeSpritePose({
          transform: { x: 0, y: 0, rotation: 0, scale: 2 },
        }),
      ),
    );
    expect(bounds).toEqual({ x: 0, y: 0, width: 200, height: 120 });
  });
});

describe('the frame looks are drawn in', () => {
  it('is the same for looks that stay near the box', () => {
    const rest = spriteRestLook(sprite);
    const near = spritePoseLook(
      sprite,
      makeSpritePose({ transform: { x: 3, y: -2, rotation: 0, scale: 1 } }),
    );
    expect(frameSpriteLooks(sprite, [rest, near])).toEqual(
      frameSpriteLooks(sprite, [rest]),
    );
  });

  it('grows to show a look that leaves the box', () => {
    const rest = spriteRestLook(sprite);
    const far = spritePoseLook(
      sprite,
      makeSpritePose({ transform: { x: 200, y: 0, rotation: 0, scale: 1 } }),
    );
    const frame = frameSpriteLooks(sprite, [rest, far]);
    expect(frame.x).toBeLessThanOrEqual(0);
    expect(frame.x + frame.width).toBeGreaterThanOrEqual(300);
    // The whole frame fits the view it is scaled for.
    const scale = spriteFrameScale(frame, { width: 240, height: 120 });
    expect(frame.width * scale).toBeLessThanOrEqual(240 + 1e-9);
    expect(frame.height * scale).toBeLessThanOrEqual(120 + 1e-9);
  });
});
