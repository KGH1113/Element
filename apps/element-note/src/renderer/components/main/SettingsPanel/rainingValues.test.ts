import { describe, expect, it } from 'vitest';
import { getDefaultNoteSettings } from '@src/renderer/defaults';
import { ownRainingValues } from './rainingValues';

describe('the Raining values of one key viewer', () => {
  const shared = getDefaultNoteSettings();

  it('keeps nothing when every value is the shared one', () => {
    expect(ownRainingValues({ ...shared }, shared)).toBeNull();
  });

  it('keeps only what differs, so the rest follows the shared values', () => {
    expect(
      ownRainingValues(
        { ...shared, speed: shared.speed + 20, reverse: !shared.reverse },
        shared,
      ),
    ).toEqual({ speed: shared.speed + 20, reverse: !shared.reverse });
  });
});
