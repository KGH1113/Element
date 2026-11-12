import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { NativeEditorDocument } from '@src/types/editor';

const invoke = vi.hoisted(() => vi.fn());
vi.mock('../invoke', () => ({
  invoke,
  trackEditorSave: (request: Promise<unknown>) => request,
  trackEditorInvocation: (request: Promise<unknown>) => request,
}));
vi.mock('./shared', () => ({ subscribe: () => () => {} }));

import { keysApi } from './keysApi';
import {
  graphItemsApi,
  knobItemsApi,
  layerGroupsApi,
  onItemPositionsSaveFailed,
  statItemsApi,
} from './itemsApi';

const saved: NativeEditorDocument = {
  schemaVersion: 1,
  keys: { hand: ['A', { keys: ['B', 'C'], match: 'all' }] },
  keyPositions: { hand: [] },
  statPositions: {},
  graphPositions: {},
  knobPositions: {},
  spritePositions: {},
  layerGroups: {},
};

/** The commands that only save, and so leave nothing to undo. */
const unrecorded = [
  'keys_update',
  'positions_update',
  'stat_positions_update',
  'graph_positions_update',
  'knob_positions_update',
  'layer_groups_update',
];

describe('every save of the editor is an undo step', () => {
  let revision: number;
  const commits = () =>
    invoke.mock.calls
      .filter(([command]) => command === 'editor_commit')
      .map(([, args]) => args.request);

  beforeEach(() => {
    revision = 3;
    invoke.mockReset().mockImplementation(async (command: string) => {
      if (command === 'editor_get') return { revision, document: saved };
      revision += 1;
      return { revision, changedFields: [] };
    });
  });

  it.each([
    ['key positions', keysApi.updatePositions, 'keyPositions'],
    ['stats', statItemsApi.updatePositions, 'statPositions'],
    ['graphs', graphItemsApi.updatePositions, 'graphPositions'],
    ['knobs', knobItemsApi.updatePositions, 'knobPositions'],
    ['layer groups', layerGroupsApi.update, 'layerGroups'],
  ] as const)('records a save of %s', async (_name, save, field) => {
    const value = { hand: [] };

    await expect(save(value as never)).resolves.toBe(value);

    expect(commits()).toHaveLength(1);
    expect(commits()[0].changes).toEqual({ schemaVersion: 1, [field]: value });
    const sent = invoke.mock.calls.map(([command]) => command);
    expect(sent.filter((command) => unrecorded.includes(command))).toEqual([]);
  });

  it('records a key change and keeps the combined key it did not touch', async () => {
    const mappings = { hand: ['D', 'B+C'] };

    await expect(keysApi.update(mappings)).resolves.toBe(mappings);

    expect(commits()).toHaveLength(1);
    expect(commits()[0]).toMatchObject({
      multiKey: true,
      changes: {
        schemaVersion: 1,
        keys: { hand: ['D', { keys: ['B', 'C'], match: 'all' }] },
      },
    });
    expect(invoke.mock.calls.map(([command]) => command)).not.toContain(
      'keys_update',
    );
  });

  it('tells who shows the saved items again when an item save fails', async () => {
    const failed = vi.fn();
    const stop = onItemPositionsSaveFailed(failed);
    const rejected = { errorCode: 'VALIDATION_FAILED', retryable: false };
    invoke.mockImplementation(async (command: string) => {
      if (command === 'editor_get') return { revision, document: saved };
      throw rejected;
    });

    await expect(statItemsApi.updatePositions({ hand: [] })).rejects.toBe(
      rejected,
    );

    expect(failed).toHaveBeenCalledExactlyOnceWith('statPositions', rejected);
    stop();
  });
});
