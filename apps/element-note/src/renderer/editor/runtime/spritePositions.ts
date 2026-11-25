/**
 * Sprites as the editor changes them. The screen is written first and the save
 * follows. While a change of ours is not answered yet, what the backend
 * reports is older than the screen and is not shown (see useSpriteStore).
 *
 * - `writeSprites` shows a list and saves it.
 * - `previewSprites` only shows it: a drag writes every frame and is saved
 *   once, by `saveSpritePreview`, when it ends. One drag is one undo step.
 * - `editSprite` changes one sprite by id and says why when it cannot.
 *
 * A save that fails puts the last saved list back on screen.
 */

import { clearEditorSaveFailure } from '@api/invoke';
import {
  validateSpriteDraft,
  type SpriteDraftError,
} from '@components/main/Modal/content/editors/spriteEditorDraft';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import type {
  ReactiveSpritePosition,
  SpritePositions,
} from '@src/types/key/sprites';
import { savableSprite, savableSprites } from '@utils/sprite/unsavedPoses';

const store = () => useSpriteStore.getState();

/** The list the backend last agreed to, kept while the screen is ahead of it. */
let saved: SpritePositions | null = null;
/** A drag is on screen and has not been saved. */
let previewing = false;
/** A save was asked for and has not been sent. */
let pending = false;
let running: Promise<void> | null = null;

function hold() {
  if (saved === null) saved = store().positions;
  // A drag holds every frame; the store is told once.
  if (!store().isLocalUpdateInProgress) store().setLocalUpdateInProgress(true);
}

function release() {
  saved = null;
  store().setLocalUpdateInProgress(false);
}

/** The first changed sprite that the backend would refuse, with the reason. */
function findUnsavable(
  next: SpritePositions,
  previous: SpritePositions | null,
): { id: string | undefined; reason: SpriteDraftError } | null {
  for (const [mode, sprites] of Object.entries(next)) {
    const before = new Set(previous?.[mode] ?? []);
    for (const sprite of sprites) {
      // What did not change was accepted before; it is not judged again.
      if (before.has(sprite)) continue;
      const reason = validateSpriteDraft(savableSprite(sprite));
      if (reason) return { id: sprite.id, reason };
    }
  }
  return null;
}

/**
 * Nothing that is saved differs: only poses that still wait for a key
 * changed. Saving that would be an undo step that undoes nothing.
 */
function isSaved(next: SpritePositions, previous: SpritePositions | null) {
  if (!previous) return false;
  const modes = new Set([...Object.keys(previous), ...Object.keys(next)]);
  for (const mode of modes) {
    const before = previous[mode] ?? [];
    const after = next[mode] ?? [];
    if (before.length !== after.length) return false;
    for (let index = 0; index < after.length; index += 1) {
      if (before[index] === after[index]) continue;
      if (
        JSON.stringify(savableSprite(before[index])) !==
        JSON.stringify(savableSprite(after[index]))
      )
        return false;
    }
  }
  return true;
}

async function run() {
  try {
    while (pending) {
      pending = false;
      const sent = store().positions;
      if (isSaved(sent, saved)) {
        saved = sent;
        continue;
      }
      const unsavable = findUnsavable(sent, saved);
      if (unsavable) {
        throw new Error(
          `Sprite ${unsavable.id ?? '(new)'} cannot be saved: ${unsavable.reason}`,
        );
      }
      await window.api.editor.commitChanges({
        spritePositions: savableSprites(sent),
      });
      saved = sent;
    }
  } catch (error) {
    console.error('Failed to save sprites', error);
    pending = false;
    previewing = false;
    if (saved) store().setLocalPositions(saved);
    // The screen shows the saved list again: nothing unsaved blocks quitting.
    clearEditorSaveFailure('editor:spritePositions');
  } finally {
    // A drag that began meanwhile keeps the screen ahead until it is saved.
    if (!previewing) release();
  }
}

function start(): Promise<void> {
  running ??= run().finally(() => {
    running = null;
    // Asked again while the last save was finishing.
    if (pending) void start();
  });
  return running;
}

function requestSave(): Promise<void> {
  previewing = false;
  pending = true;
  return start();
}

/** The same sprites in the same order: writing this list changes nothing. */
function isOnScreen(next: SpritePositions): boolean {
  const current = store().positions;
  if (next === current) return true;
  return [...new Set([...Object.keys(current), ...Object.keys(next)])].every(
    (mode) => {
      const before = current[mode] ?? [];
      const after = next[mode] ?? [];
      return (
        before.length === after.length &&
        before.every((sprite, index) => sprite === after[index])
      );
    },
  );
}

/** Shows the list and saves it. A list that changes nothing is not saved. */
export function writeSprites(next: SpritePositions): Promise<void> {
  if (isOnScreen(next)) return Promise.resolve();
  hold();
  store().setLocalPositions(next);
  return requestSave();
}

/** Shows the list without saving it yet. */
export function previewSprites(next: SpritePositions): void {
  if (isOnScreen(next)) return;
  hold();
  previewing = true;
  store().setLocalPositions(next);
}

/** Saves what `previewSprites` left on screen. Nothing to do without one. */
export function saveSpritePreview(): Promise<void> {
  return previewing ? requestSave() : Promise.resolve();
}

/** Changes the sprites of one tab. */
export function writeSpritesOf(
  mode: string,
  change: (sprites: ReactiveSpritePosition[]) => ReactiveSpritePosition[],
  options: { save?: boolean } = {},
): Promise<void> {
  const positions = store().positions;
  const current = positions[mode] ?? [];
  const next = change(current);
  if (next === current) return Promise.resolve();
  const nextPositions = { ...positions, [mode]: next };
  if (options.save === false) {
    previewSprites(nextPositions);
    return Promise.resolve();
  }
  return writeSprites(nextPositions);
}

/**
 * Changes one sprite, found by its id so that a sprite removed meanwhile is
 * not brought back. Returns why the change cannot be saved; then nothing is
 * written, and the caller keeps what was typed and says what is wrong.
 *
 * With `save: false` the change is only shown, as a drag on the canvas is:
 * every step of a drag in the sidebar is written this way and the whole drag
 * is saved once, by `saveSpritePreview`, when it ends.
 */
export function editSprite(
  mode: string,
  id: string | undefined,
  change: (sprite: ReactiveSpritePosition) => ReactiveSpritePosition,
  options?: { save?: boolean },
): SpriteDraftError | null {
  const sprites = store().positions[mode] ?? [];
  const current = sprites.find((sprite) => sprite.id === id);
  if (!current) return null;
  const next = change(current);
  if (next === current) return null;
  const reason = validateSpriteDraft(savableSprite(next));
  if (reason) return reason;
  void writeSpritesOf(
    mode,
    (list) => list.map((sprite) => (sprite === current ? next : sprite)),
    options,
  );
  return null;
}

/** Changes the sprites at these places of one tab's list. */
export function patchSpritesAt(
  mode: string,
  indexes: readonly number[],
  patch: (
    sprite: ReactiveSpritePosition,
    index: number,
  ) => Partial<ReactiveSpritePosition>,
  options?: { save?: boolean },
): Promise<void> {
  const places = new Set(indexes);
  return writeSpritesOf(
    mode,
    (sprites) =>
      sprites.some((_, index) => places.has(index))
        ? sprites.map((sprite, index) =>
            places.has(index) ? { ...sprite, ...patch(sprite, index) } : sprite,
          )
        : sprites,
    options,
  );
}

/** Removes the sprites at these places of one tab's list. */
export function removeSpritesAt(
  mode: string,
  indexes: readonly number[],
): Promise<void> {
  const places = new Set(indexes);
  return writeSpritesOf(mode, (sprites) =>
    sprites.some((_, index) => places.has(index))
      ? sprites.filter((_, index) => !places.has(index))
      : sprites,
  );
}
