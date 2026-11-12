import { describe, expect, it, vi } from 'vitest';
import type { invoke } from '@tauri-apps/api/core';
import type { EditorSnapshot, NativeEditorDocument } from '@src/types/editor';
import { createDefaultKeyPosition } from '@src/renderer/editor/model/keys';
import { startHistorySteps } from '../historyStep';
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

describe('saves of what the screen shows', () => {
  type Request = {
    baseRevision: number;
    gestureId?: string;
    changes: Record<string, unknown>;
  };

  /** A backend that keeps a revision and answers when the test lets it. */
  function backend(startRevision = 7) {
    let revision = startRevision;
    const commands: string[] = [];
    const requests: Request[] = [];
    const held: (() => void)[] = [];
    let hold = false;
    const invokeCommand = vi.fn(
      async (command: string, args?: { request: Request }) => {
        commands.push(command);
        if (command === 'editor_get') return { revision, document: document() };
        requests.push(args!.request);
        if (hold) await new Promise<void>((resolve) => held.push(resolve));
        if (args!.request.baseRevision !== revision) {
          throw {
            errorCode: 'REVISION_CONFLICT',
            details: { currentRevision: revision },
            retryable: true,
          };
        }
        revision += 1;
        return { revision, changedFields: Object.keys(args!.request.changes) };
      },
    );
    return {
      invokeCommand: invokeCommand as unknown as typeof invoke,
      commands,
      requests,
      holdCommits() {
        hold = true;
      },
      release() {
        hold = false;
        held.splice(0).forEach((resolve) => resolve());
      },
      saveElsewhere() {
        revision += 1;
      },
    };
  }

  const positions = (dx: number) => ({
    hand: [{ ...createDefaultKeyPosition(), id: 'first', dx }],
  });

  it('sends one request per save once it knows the revision', async () => {
    const native = backend();
    const api = createEditorApi(native.invokeCommand, () => undefined);

    await api.replaceFields({ keyPositions: positions(1) });
    await api.replaceFields({ keyPositions: positions(2) });
    await api.replaceFields({ layerGroups: {} });

    expect(native.commands).toEqual([
      'editor_get',
      'editor_commit',
      'editor_commit',
      'editor_commit',
    ]);
    expect(native.requests.map((request) => request.baseRevision)).toEqual([
      7, 8, 9,
    ]);
  });

  it('sends the values as they are, without reading the document', async () => {
    const native = backend();
    const api = createEditorApi(native.invokeCommand, () => undefined);
    const sent = positions(40);

    await api.replaceFields({ keyPositions: sent });

    // 'first' has a rotation in the saved document: a replaced field drops it.
    expect(native.requests[0].changes).toEqual({
      schemaVersion: 1,
      keyPositions: sent,
    });
    expect(native.requests[0]).not.toHaveProperty('gestureId');
  });

  it('marks every save of one undo step with the same gesture', async () => {
    const native = backend();
    let step = '11111111-1111-4111-8111-111111111111';
    const api = createEditorApi(native.invokeCommand, () => step);

    await api.replaceFields({ keyPositions: positions(1) });
    await api.replaceFields({ statPositions: {} });
    await api.commitChanges({ keys: { hand: ['A', 'B+C'] } });
    step = '22222222-2222-4222-8222-222222222222';
    await api.replaceFields({ keyPositions: positions(2) });

    expect(native.requests.map((request) => request.gestureId)).toEqual([
      '11111111-1111-4111-8111-111111111111',
      '11111111-1111-4111-8111-111111111111',
      '11111111-1111-4111-8111-111111111111',
      '22222222-2222-4222-8222-222222222222',
    ]);
  });

  it('saves again at the new revision when something else saved first', async () => {
    const native = backend();
    const api = createEditorApi(native.invokeCommand, () => undefined);
    await api.replaceFields({ keyPositions: positions(1) });

    // An undo, a preset or another window moved the revision on.
    native.saveElsewhere();
    await expect(
      api.replaceFields({ keyPositions: positions(2) }),
    ).resolves.toMatchObject({ revision: 10 });

    expect(native.commands).toEqual([
      'editor_get',
      'editor_commit',
      'editor_commit',
      'editor_commit',
    ]);
    expect(native.requests.map((request) => request.baseRevision)).toEqual([
      7, 8, 9,
    ]);
  });

  it('reads the revision again after a save that failed for another reason', async () => {
    const native = backend();
    const rejected = { errorCode: 'VALIDATION_FAILED', retryable: false };
    let fail = false;
    const invokeCommand = vi.fn(async (command: string, args?: unknown) => {
      if (command === 'editor_commit' && fail) {
        fail = false;
        throw rejected;
      }
      return (native.invokeCommand as (...input: unknown[]) => unknown)(
        command,
        args,
      );
    });
    const api = createEditorApi(
      invokeCommand as typeof invoke,
      () => undefined,
    );
    await api.replaceFields({ keyPositions: positions(1) });

    fail = true;
    await expect(
      api.replaceFields({ keyPositions: positions(2) }),
    ).rejects.toBe(rejected);
    await api.replaceFields({ keyPositions: positions(3) });

    expect(native.commands).toEqual([
      'editor_get',
      'editor_commit',
      'editor_get',
      'editor_commit',
    ]);
  });

  it('sends only the newest value when saves of a field pile up in one step', async () => {
    const native = backend();
    const api = createEditorApi(native.invokeCommand, () => 'scrub');
    await api.replaceFields({ keyPositions: positions(0) });

    native.holdCommits();
    const sending = api.replaceFields({ keyPositions: positions(1) });
    await vi.waitFor(() => expect(native.requests).toHaveLength(2));
    const ticks = [2, 3, 4].map((dx) =>
      api.replaceFields({ keyPositions: positions(dx) }),
    );
    native.release();
    await Promise.all([sending, ...ticks]);

    // The first tick was already on its way; the other three became one.
    expect(native.requests.slice(1).map((request) => request.changes)).toEqual([
      { schemaVersion: 1, keyPositions: positions(1) },
      { schemaVersion: 1, keyPositions: positions(4) },
    ]);
  });

  it('keeps saves of other fields and other steps apart', async () => {
    const native = backend();
    let step = 'drag';
    const api = createEditorApi(native.invokeCommand, () => step);
    await api.replaceFields({ keyPositions: positions(0) });

    native.holdCommits();
    const first = api.replaceFields({ keyPositions: positions(1) });
    await vi.waitFor(() => expect(native.requests).toHaveLength(2));
    const waiting = api.replaceFields({ keyPositions: positions(2) });
    const otherField = api.replaceFields({ statPositions: {} });
    step = 'next drag';
    const otherStep = api.replaceFields({ statPositions: {} });
    native.release();
    await Promise.all([first, waiting, otherField, otherStep]);

    expect(
      native.requests
        .slice(1)
        .map((request) => [
          request.gestureId,
          Object.keys(request.changes).filter((key) => key !== 'schemaVersion'),
        ]),
    ).toEqual([
      ['drag', ['keyPositions']],
      ['drag', ['keyPositions']],
      ['drag', ['statPositions']],
      ['next drag', ['statPositions']],
    ]);
  });

  it('does not move a waiting save past a commit queued after it', async () => {
    const native = backend();
    const api = createEditorApi(native.invokeCommand, () => 'step');
    await api.replaceFields({ keyPositions: positions(0) });

    native.holdCommits();
    const first = api.replaceFields({ keyPositions: positions(1) });
    await vi.waitFor(() => expect(native.requests).toHaveLength(2));
    const waiting = api.replaceFields({ keyPositions: positions(2) });
    const added = api.commitChanges({
      keys: { hand: ['A', 'B+C', 'D'] },
      keyPositions: { hand: [] },
    });
    const after = api.replaceFields({ keyPositions: positions(3) });
    native.release();
    await Promise.all([first, waiting, added, after]);

    expect(
      native.requests
        .slice(1)
        .map((request) => Object.keys(request.changes).length),
    ).toEqual([2, 2, 3, 2]);
    expect(native.requests[2].changes.keyPositions).toEqual(positions(2));
    expect(native.requests[4].changes.keyPositions).toEqual(positions(3));
  });

  it('records what one press saves as one step and the next press as another', async () => {
    const native = backend();
    const api = createEditorApi(native.invokeCommand);
    const stop = startHistorySteps();
    const press = () => window.dispatchEvent(new Event('pointerdown'));

    // A drag of a mixed selection saves each kind when it is let go.
    press();
    await Promise.all([
      api.replaceFields({ keyPositions: positions(5) }),
      api.replaceFields({ statPositions: {} }),
      api.commitChanges({ spritePositions: {} }),
      api.replaceFields({ layerGroups: {} }),
    ]);
    press();
    await api.replaceFields({ keyPositions: positions(9) });
    stop();

    const steps = native.requests.map((request) => request.gestureId);
    expect(new Set(steps.slice(0, 4)).size).toBe(1);
    expect(steps[0]).toMatch(/^[0-9a-f-]{36}$/);
    expect(steps[4]).toMatch(/^[0-9a-f-]{36}$/);
    expect(steps[4]).not.toBe(steps[0]);
  });
});
