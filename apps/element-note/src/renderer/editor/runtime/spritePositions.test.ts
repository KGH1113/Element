import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { EditorChanges } from '@src/types/editor';
import type { SpritePositions } from '@src/types/key/sprites';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import {
  makeSpritePose,
  makeSpritePosition,
} from '@utils/sprite/spriteFixtures';
import {
  editSprite,
  patchSpritesAt,
  previewSprites,
  removeSpritesAt,
  saveSpritePreview,
  writeSprites,
} from './spritePositions';

/** A save the test answers by hand, to stand between a change and its echo. */
function deferred() {
  let resolve!: () => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<void>((done, fail) => {
    resolve = done;
    reject = fail;
  });
  return { promise, resolve, reject };
}

const sprite = (id: string, dx = 0) =>
  makeSpritePosition({ id, dx, zIndex: 0 });
const screen = () => useSpriteStore.getState().positions;
const savedLists = (commit: ReturnType<typeof vi.fn>) =>
  commit.mock.calls.map(
    ([changes]) => (changes as EditorChanges).spritePositions!,
  );

describe('saving sprites', () => {
  const originalApi = window.api;
  const commitChanges = vi.fn(async (_changes: EditorChanges) => undefined);
  const first = sprite('first');
  const second = sprite('second');
  const foot = [sprite('foot')];
  let start: SpritePositions;

  beforeEach(() => {
    commitChanges.mockReset();
    commitChanges.mockResolvedValue(undefined);
    window.api = {
      editor: { commitChanges },
    } as unknown as typeof window.api;
    start = { hand: [first, second], foot };
    useSpriteStore.setState({
      positions: start,
      isLocalUpdateInProgress: false,
    });
  });
  afterEach(() => {
    window.api = originalApi;
    vi.restoreAllMocks();
  });

  it('shows the change first and then saves it', async () => {
    const pending = deferred();
    commitChanges.mockReturnValueOnce(pending.promise);
    const next = { ...start, hand: [{ ...first, dx: 42 }, second] };

    const saving = writeSprites(next);
    expect(screen()).toBe(next);
    expect(commitChanges).toHaveBeenCalledTimes(1);
    expect(savedLists(commitChanges)[0].hand[0].dx).toBe(42);
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(true);

    pending.resolve();
    await saving;
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(false);
    expect(screen()).toBe(next);
  });

  it('does not save a list that changes nothing', async () => {
    await writeSprites({ hand: [first, second], foot, empty: [] });
    expect(commitChanges).not.toHaveBeenCalled();
    expect(screen()).toBe(start);
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(false);
  });

  it('puts the saved list back and logs when the save fails', async () => {
    const logged = vi.spyOn(console, 'error').mockImplementation(() => {});
    commitChanges.mockRejectedValueOnce(new Error('rejected'));

    await writeSprites({ ...start, hand: [{ ...first, dx: 42 }, second] });
    expect(screen()).toBe(start);
    expect(logged).toHaveBeenCalledWith(
      'Failed to save sprites',
      expect.any(Error),
    );
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(false);

    // The next change is saved as usual.
    await writeSprites({ ...start, hand: [{ ...first, dx: 7 }, second] });
    expect(commitChanges).toHaveBeenCalledTimes(2);
    expect(screen().hand[0].dx).toBe(7);
  });

  it('does not send a sprite the backend would refuse', async () => {
    const logged = vi.spyOn(console, 'error').mockImplementation(() => {});
    await writeSprites({ ...start, hand: [{ ...first, width: 0 }, second] });
    expect(commitChanges).not.toHaveBeenCalled();
    expect(screen()).toBe(start);
    expect(logged).toHaveBeenCalled();
  });

  it('ignores the echo of an older state while a save is on its way', async () => {
    const pending = deferred();
    commitChanges.mockReturnValueOnce(pending.promise);
    const next = { ...start, hand: [{ ...first, dx: 42 }, second] };
    const saving = writeSprites(next);

    useSpriteStore.getState().setPositions(start);
    expect(screen()).toBe(next);

    pending.resolve();
    await saving;
    // Once the save is answered, the backend is believed again.
    const reported = { ...start, hand: [{ ...first, dx: 42 }, second] };
    useSpriteStore.getState().setPositions(reported);
    expect(screen()).toBe(reported);
  });

  it('saves a burst of changes as the first and the last', async () => {
    const pending = deferred();
    commitChanges.mockReturnValueOnce(pending.promise);
    const at = (dx: number) => ({
      ...start,
      hand: [{ ...first, dx }, second],
    });

    const saving = writeSprites(at(1));
    void writeSprites(at(2));
    void writeSprites(at(3));
    // An echo of the first save must not undo the two that followed.
    useSpriteStore.getState().setPositions(at(1));
    expect(screen().hand[0].dx).toBe(3);

    pending.resolve();
    await saving;
    expect(savedLists(commitChanges).map((list) => list.hand[0].dx)).toEqual([
      1, 3,
    ]);
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(false);
  });

  it('saves a drag once, when it ends', async () => {
    for (const dx of [1, 2, 3]) {
      previewSprites({ ...start, hand: [{ ...first, dx }, second] });
      // What the backend reports during the drag is older than the screen.
      useSpriteStore.getState().setPositions(start);
      expect(screen().hand[0].dx).toBe(dx);
    }
    expect(commitChanges).not.toHaveBeenCalled();

    await saveSpritePreview();
    expect(commitChanges).toHaveBeenCalledTimes(1);
    expect(savedLists(commitChanges)[0].hand[0].dx).toBe(3);
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(false);

    // Nothing was dragged since: ending again saves nothing.
    await saveSpritePreview();
    expect(commitChanges).toHaveBeenCalledTimes(1);
  });

  it('shows each step of a drag on one sprite and saves the drag once', async () => {
    for (const dx of [1, 2, 3]) {
      expect(
        editSprite('hand', 'first', (item) => ({ ...item, dx }), {
          save: false,
        }),
      ).toBeNull();
      expect(screen().hand[0].dx).toBe(dx);
    }
    expect(commitChanges).not.toHaveBeenCalled();

    // A step that cannot be saved is not shown either.
    expect(
      editSprite('hand', 'first', (item) => ({ ...item, width: -1 }), {
        save: false,
      }),
    ).toBe('range');
    expect(screen().hand[0].width).toBe(first.width);

    await saveSpritePreview();
    expect(commitChanges).toHaveBeenCalledTimes(1);
    expect(savedLists(commitChanges)[0].hand[0].dx).toBe(3);
  });

  it('moves and removes sprites by their place without touching the others', async () => {
    await patchSpritesAt('hand', [1], (item) => ({ dx: item.dx + 5 }));
    expect(screen().hand[0]).toBe(first);
    expect(screen().hand[1]).toMatchObject({ id: 'second', dx: 5 });
    expect(screen().foot).toBe(foot);

    await removeSpritesAt('hand', [0]);
    expect(screen().hand.map((item) => item.id)).toEqual(['second']);
    expect(commitChanges).toHaveBeenCalledTimes(2);

    // A place that holds nothing is not a change.
    await patchSpritesAt('hand', [9], () => ({ dx: 1 }));
    await removeSpritesAt('none', [0]);
    expect(commitChanges).toHaveBeenCalledTimes(2);
  });

  it('edits one sprite by its id and keeps other tabs and sprites as they are', () => {
    expect(editSprite('hand', 'first', (item) => ({ ...item, dx: 42 }))).toBe(
      null,
    );
    expect(screen().hand[0]).toMatchObject({ id: 'first', dx: 42 });
    expect(screen().hand[1]).toBe(second);
    expect(screen().foot).toBe(foot);
    expect(first.dx).toBe(0);
  });

  it('does not bring back a sprite that was removed meanwhile', () => {
    expect(editSprite('hand', 'removed', (item) => ({ ...item, dx: 1 }))).toBe(
      null,
    );
    expect(screen()).toBe(start);
    expect(commitChanges).not.toHaveBeenCalled();
  });

  it('says why a change cannot be saved and writes nothing', () => {
    expect(
      editSprite('hand', 'first', (item) => ({
        ...item,
        transitionEasing: ' ',
      })),
    ).toBe('easing');
    expect(
      editSprite('hand', 'first', (item) => ({
        ...item,
        poses: [
          makeSpritePose({ poseId: 'one', triggers: ['a'] }),
          makeSpritePose({ poseId: 'two', triggers: ['a'] }),
        ],
      })),
    ).toBe('duplicateTriggers');
    expect(screen()).toBe(start);
    expect(commitChanges).not.toHaveBeenCalled();
  });

  it('keeps a pose that needs a key on screen without saving it', async () => {
    const pose = makeSpritePose({ poseId: 'new', triggers: [] });
    expect(
      editSprite('hand', 'first', (item) => ({ ...item, poses: [pose] })),
    ).toBe(null);
    await Promise.resolve();
    expect(screen().hand[0].poses).toEqual([pose]);
    // Nothing that is saved changed, so there is nothing to save.
    expect(commitChanges).not.toHaveBeenCalled();
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(false);

    // Another change of the sprite is saved; the pose stays behind.
    editSprite('hand', 'first', (item) => ({ ...item, dx: 5 }));
    expect(savedLists(commitChanges)[0].hand[0]).toMatchObject({
      dx: 5,
      poses: [],
    });
    await Promise.resolve();
    await Promise.resolve();

    // What the backend reports does not have the pose; it is kept.
    useSpriteStore.getState().setPositions({
      ...start,
      hand: [{ ...first, dx: 5 }, second],
    });
    expect(screen().hand[0].poses).toEqual([pose]);

    // With a key it is saved like any other pose.
    editSprite('hand', 'first', (item) => ({
      ...item,
      poses: item.poses.map((each) => ({ ...each, triggers: ['key-a'] })),
    }));
    expect(savedLists(commitChanges)[1].hand[0].poses).toEqual([
      { ...pose, triggers: ['key-a'] },
    ]);
  });
});
