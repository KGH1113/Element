/**
 * Ghost keys: a second keyboard key bound to the same element.
 *
 * The ghost renders as its own virtual key on top of the owner with the
 * owner's geometry, so every viewer (overlay, OBS, in-game) reuses the normal
 * key, note and counter pipeline. The key limiter never includes it.
 */

import type {
  GhostKey,
  GhostKeySettings,
  KeyPosition,
  NoteColor,
} from '@src/types/key/keys';
import { normalizeCounterSettings } from '@src/types/key/keys';

const TRANSPARENT = 'rgba(0, 0, 0, 0)';
const HEX_COLOR = /^#([0-9a-f]{6})$/i;
const RGB_COLOR = /^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)/i;

/** The same color with zero alpha, so a later opacity change keeps its hue. */
export function toTransparentColor(color: string | undefined): string {
  if (!color) return TRANSPARENT;
  const hex = HEX_COLOR.exec(color.trim());
  if (hex) {
    const value = parseInt(hex[1], 16);
    return `rgba(${(value >> 16) & 255}, ${(value >> 8) & 255}, ${value & 255}, 0)`;
  }
  const rgb = RGB_COLOR.exec(color.trim());
  return rgb ? `rgba(${rgb[1]}, ${rgb[2]}, ${rgb[3]}, 0)` : TRANSPARENT;
}

function noteBorderHex(color: NoteColor): string {
  const value = typeof color === 'string' ? color : color?.top;
  return typeof value === 'string' && HEX_COLOR.test(value.trim())
    ? value.trim().toUpperCase()
    : '#FFFFFF';
}

/**
 * Starting look when a ghost is enabled: invisible while idle, the owner's
 * pressed colors while held, and an outline-only note in the owner's color.
 */
export function createDefaultGhostSettings(
  owner: KeyPosition,
): GhostKeySettings {
  const { ghost: _ghost, layerName: _layer, groupId: _group, ...base } = owner;
  const gradient = typeof owner.noteColor !== 'string';
  return {
    ...base,
    hidden: false,
    count: 0,
    keyLimiterExcluded: false,
    idleTransparent: true,
    inactiveImage: '',
    backgroundColor: toTransparentColor(owner.backgroundColor),
    borderColor: toTransparentColor(owner.borderColor),
    fontColor: toTransparentColor(owner.fontColor),
    displayText: undefined,
    noteOpacity: 0,
    noteOpacityTop: gradient ? 0 : undefined,
    noteOpacityBottom: gradient ? 0 : undefined,
    noteBorderColor: noteBorderHex(owner.noteColor),
    noteBorderWidth: 3,
    noteBorderOpacity: 90,
    noteBorderSide: undefined,
    counter: {
      ...normalizeCounterSettings(owner.counter),
      enabled: false,
    },
  };
}

export function hasActiveGhost(
  position: KeyPosition | undefined,
  ownerKey: string,
): boolean {
  const ghost = position?.ghost;
  return Boolean(
    ghost?.enabled &&
    typeof ghost.key === 'string' &&
    ghost.key.length > 0 &&
    ghost.key !== ownerKey,
  );
}

/**
 * Ghost settings with the owner's live geometry and stacking. Its label is
 * its own key's unless the ghost sets display text: while held, it is a
 * different key in the same place.
 */
export function resolveGhostPosition(
  owner: KeyPosition,
  ownerIndex: number,
): KeyPosition {
  const settings = owner.ghost!.settings;
  return {
    ...settings,
    dx: owner.dx,
    dy: owner.dy,
    width: owner.width,
    height: owner.height,
    hidden: owner.hidden,
    // Same z-index as the owner; ghosts come after every key, so the ghost
    // stacks directly above its owner.
    zIndex: owner.zIndex ?? ownerIndex,
    displayText: settings.displayText || undefined,
    layerName: undefined,
    groupId: undefined,
    keyLimiterExcluded: true,
  };
}

export interface ExpandedKeys {
  keys: string[];
  positions: KeyPosition[];
  /**
   * For each entry, the key sharing its element: an owner's ghost or a
   * ghost's owner. Null when the tab has no ghosts.
   */
  pairedKeys: Array<string | undefined> | null;
}

/**
 * Appends one virtual key per enabled ghost. Returns the inputs unchanged
 * when a tab has no ghosts, so memoized consumers keep their identity.
 */
export function expandGhostKeys(
  keys: string[],
  positions: KeyPosition[],
): ExpandedKeys {
  let expandedKeys: string[] | null = null;
  let expandedPositions: KeyPosition[] | null = null;
  let pairedKeys: Array<string | undefined> | null = null;
  for (let index = 0; index < keys.length; index++) {
    const owner = positions[index];
    if (!hasActiveGhost(owner, keys[index])) continue;
    expandedKeys ??= keys.slice();
    // Keep ghosts index-aligned with their keys even when a key has no
    // stored position; viewers fall back for those the same way as before.
    expandedPositions ??= Array.from(
      { length: keys.length },
      (_, i) => positions[i],
    );
    pairedKeys ??= new Array<string | undefined>(keys.length);
    const ghostKey = owner.ghost!.key!;
    pairedKeys[index] = ghostKey;
    pairedKeys.push(keys[index]);
    expandedKeys.push(ghostKey);
    expandedPositions.push(resolveGhostPosition(owner, index));
  }
  return expandedKeys
    ? { keys: expandedKeys, positions: expandedPositions!, pairedKeys }
    : { keys, positions, pairedKeys: null };
}

/**
 * A key and its ghost share one element and the most recent press wins: an
 * element hides while its pair is held and was pressed after it.
 */
export function isCoveredByPair(
  active: boolean,
  pairActive: boolean,
  pressOrder: number,
  pairPressOrder: number,
): boolean {
  return pairActive && (!active || pairPressOrder > pressOrder);
}

/** Keyboard keys bound as ghosts in one tab, in key order. */
export function ghostKeyLabels(
  keys: string[],
  positions: KeyPosition[],
): string[] {
  const labels: string[] = [];
  for (let index = 0; index < keys.length; index++) {
    const position = positions[index];
    if (hasActiveGhost(position, keys[index]))
      labels.push(position.ghost!.key!);
  }
  return labels;
}

/** Merges a partial settings change into the current ghost. */
export function mergeGhostSettings(
  owner: KeyPosition,
  updates: Partial<GhostKeySettings>,
): GhostKey {
  const current = owner.ghost ?? {
    enabled: false,
    key: null,
    settings: createDefaultGhostSettings(owner),
  };
  return {
    ...current,
    settings: { ...current.settings, ...updates },
  };
}
