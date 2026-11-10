import { describe, expect, it, vi } from 'vitest';
import type { invoke } from '@tauri-apps/api/core';
import type { EditorSnapshot, NativeEditorDocument } from '@src/types/editor';
import { createDefaultKeyPosition } from '@src/renderer/editor/model/keys';
import {
  createEditorApi,
  prepareEditorChanges,
  projectNativeKeyMappings,
  snapshotPluginEditorRequest,
} from './editorApi';

function document(): NativeEditorDocument {
  return {
    schemaVersion: 1,
    keys: { hand: ['A', { keys: ['B', 'C'], match: 'all' }] },
    keyPositions: {
      hand: [
        { id: 'first', dx: 10, rotation: 15 },
        { id: 'second', dx: 80, imageMode: 'overlay' },
      ],
    },
    statPositions: {},
    graphPositions: {},
    knobPositions: {},
    spritePositions: { hand: [{ id: 'sprite', poses: [] }] },
    layerGroups: {},
  };
}

describe('v2 editor compatibility', () => {
  it('projects compound preset bindings to native canonical key labels', () => {
    expect(
      projectNativeKeyMappings({
        hand: [
          '',
          'A',
          { keys: ['B', 'C'], match: 'all' },
          { keys: ['D', 'E'], match: 'any' },
        ],
      }),
    ).toEqual({ hand: ['', 'A', 'B+C', 'D|E'] });
  });
  it('keeps compound slots and v2-only properties through legacy edits', () => {
    const source = document();
    const patch = prepareEditorChanges(
      {
        keys: { hand: ['B+C', 'A', 'D'] },
        keyPositions: {
          hand: [
            { ...createDefaultKeyPosition(), id: 'second', dx: 0 },
            { ...createDefaultKeyPosition(), id: 'first', dx: 70 },
            createDefaultKeyPosition(140),
          ],
        },
      },
      source,
    );

    expect(patch.keys?.hand).toEqual([
      { keys: ['B', 'C'], match: 'all' },
      'A',
      'D',
    ]);
    expect(patch.keyPositions?.hand[0]).toMatchObject({
      id: 'second',
      dx: 0,
      imageMode: 'overlay',
    });
    expect(patch.keyPositions?.hand[1]).toMatchObject({
      id: 'first',
      dx: 70,
      rotation: 15,
    });
    expect(patch).not.toHaveProperty('spritePositions');
    expect(source.keyPositions.hand[0].dx).toBe(10);
  });

  it('does not restore a compound slot when its binding is changed', () => {
    expect(
      prepareEditorChanges({ keys: { hand: ['A', 'D'] } }, document()).keys,
    ).toEqual({ hand: ['A', 'D'] });
  });

  it('commits coupled fields together and reads the next revision after commit', async () => {
    let revision = 5;
    const calls: string[] = [];
    const invokeCommand = vi.fn(async (command: string, _args?: unknown) => {
      calls.push(command);
      if (command === 'editor_get') {
        return { revision, document: document() } satisfies EditorSnapshot;
      }
      revision += 1;
      return { revision, changedFields: ['keys', 'keyPositions'] };
    });
    const api = createEditorApi(invokeCommand as typeof invoke);
    const changes = {
      keys: { hand: ['D'] },
      keyPositions: { hand: [createDefaultKeyPosition()] },
    };
    const first = api.commitChanges(changes);
    changes.keys.hand[0] = 'E';
    const second = api.commitChanges(changes);
    await Promise.all([first, second]);

    expect(calls).toEqual([
      'editor_get',
      'editor_commit',
      'editor_get',
      'editor_commit',
    ]);
    expect(invokeCommand.mock.calls[1][1]).toMatchObject({
      request: {
        baseRevision: 5,
        multiKey: true,
        changes: { schemaVersion: 1, keys: { hand: ['D'] }, keyPositions: {} },
      },
    });
    expect(invokeCommand.mock.calls[3][1]).toMatchObject({
      request: { baseRevision: 6, changes: { keys: { hand: ['E'] } } },
    });
  });

  it('reports rejected commits without blocking subsequent edits', async () => {
    const rejected = { errorCode: 'REVISION_CONFLICT', retryable: true };
    let fail = true;
    const invokeCommand = vi.fn(async (command: string) => {
      if (command === 'editor_get') {
        return { revision: 4, document: document() };
      }
      if (fail) {
        fail = false;
        throw rejected;
      }
      return { revision: 5, changedFields: ['keys'] };
    });
    const api = createEditorApi(invokeCommand as typeof invoke);
    await expect(api.commitChanges({ keys: { hand: ['A'] } })).rejects.toBe(
      rejected,
    );
    await expect(api.commitChanges({ keys: { hand: ['D'] } })).resolves.toEqual(
      { revision: 5, changedFields: ['keys'] },
    );
  });

  it('validates both the original public request and its JSON wire shape', () => {
    const request = {
      baseRevision: 1,
      mutationId: 'plugin',
      changes: { schemaVersion: 1 },
    };
    expect(() =>
      snapshotPluginEditorRequest({ ...request, ops: undefined }),
    ).toThrow("unsupported key 'ops'");
    const withToJSON = Object.defineProperty({ ...request }, 'toJSON', {
      value: () => ({ ...request, ops: [] }),
    });
    expect(() => snapshotPluginEditorRequest(withToJSON)).toThrow(
      "unsupported key 'ops'",
    );
    expect(() =>
      snapshotPluginEditorRequest({
        ...request,
        changes: { schemaVersion: 2 },
      }),
    ).toThrow('schemaVersion must be 1');
  });

  it('cancels queued plugin writes after revocation without blocking UI edits', async () => {
    let release: (snapshot: EditorSnapshot) => void;
    const firstSnapshot = new Promise<EditorSnapshot>((resolve) => {
      release = resolve;
    });
    let gets = 0;
    const invokeCommand = vi.fn(async (command: string) => {
      if (command === 'editor_get') {
        gets += 1;
        return gets === 1
          ? firstSnapshot
          : { revision: gets, document: document() };
      }
      return { revision: 2, changedFields: ['keys'] };
    });
    const api = createEditorApi(invokeCommand as typeof invoke);
    let active = true;
    const first = api.commitChanges({ keys: { hand: ['A'] } });
    const plugin = api.commitNativeChanges(
      { keys: { hand: ['B'] } },
      false,
      () => active,
    );
    const rejected = expect(plugin).rejects.toThrow('no longer active');
    const last = api.commitChanges({ keys: { hand: ['C'] } });
    active = false;
    release({ revision: 1, document: document() });
    await Promise.all([first, rejected, last]);
    expect(invokeCommand.mock.calls.map(([command]) => command)).toEqual([
      'editor_get',
      'editor_commit',
      'editor_get',
      'editor_commit',
    ]);
  });

  it('freezes public plugin changes without rewriting their explicit revision or opt-in', async () => {
    const invokeCommand = vi.fn(async () => ({
      revision: 3,
      changedFields: ['keys'],
    }));
    const api = createEditorApi(invokeCommand as typeof invoke);
    const request = {
      baseRevision: 2,
      mutationId: 'exact-id',
      multiKey: true,
      changes: {
        schemaVersion: 1 as const,
        keys: { hand: [{ keys: ['A', 'B'], match: 'all' as const }] },
      },
    };
    const pending = api.commit(request);
    request.changes.keys.hand[0].keys[0] = 'C';
    await pending;
    expect(invokeCommand).toHaveBeenCalledWith('editor_commit', {
      request: {
        baseRevision: 2,
        mutationId: 'exact-id',
        multiKey: true,
        changes: {
          schemaVersion: 1,
          keys: { hand: [{ keys: ['A', 'B'], match: 'all' }] },
        },
      },
    });
  });
});
