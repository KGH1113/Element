import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { installFrozenShim } from './frozen-shim.mjs';

let dispose: (() => void) | undefined;
beforeEach(() => {
  vi.stubGlobal('localStorage', { setItem: vi.fn() });
});
afterEach(() => {
  dispose?.();
  dispose = undefined;
  vi.unstubAllGlobals();
});

function frozenInvoke() {
  return (
    globalThis as typeof globalThis & {
      __dmnRenderInternals: {
        invoke: (
          command: string,
          args?: Record<string, unknown>,
        ) => Promise<unknown>;
      };
    }
  ).__dmnRenderInternals.invoke;
}

function install() {
  const snapshot = {
    settings: { language: 'ko', noteSettings: { frameLimit: 60 } },
    keys: { hand: [{ keys: ['A', 'B'], match: 'all' }] },
    positions: { hand: [{ id: 'key', width: 60 }] },
    spritePositions: { hand: [{ id: 'sprite', activePoseId: 'rest' }] },
  };
  const pluginStorage = {
    'plugin/instances': [{ instanceId: 'saved', position: { x: 3, y: 7 } }],
  };
  const shim = installFrozenShim(snapshot, 'hand', {}, pluginStorage);
  dispose = shim.dispose;
  const internals = (
    globalThis as typeof globalThis & {
      __dmnRenderInternals: {
        invoke: (
          command: string,
          args?: Record<string, unknown>,
        ) => Promise<unknown>;
      };
    }
  ).__dmnRenderInternals;
  return { invoke: internals.invoke, snapshot, pluginStorage };
}

describe('frozen v2 plugin API', () => {
  it('restores native compound bindings and sprite data without touching the source snapshot', async () => {
    const { invoke, snapshot } = install();
    const first = (await invoke('editor_get')) as {
      document: {
        keys: typeof snapshot.keys;
        spritePositions: typeof snapshot.spritePositions;
      };
    };
    expect(first.document.keys).toEqual(snapshot.keys);
    expect(first.document.spritePositions).toEqual(snapshot.spritePositions);
    first.document.keys.hand[0].keys[0] = 'C';
    expect(await invoke('editor_get')).toMatchObject({
      revision: 0,
      document: { schemaVersion: 1, keys: snapshot.keys },
    });
    expect(snapshot.settings.noteSettings.frameLimit).toBe(60);
  });

  it('loads isolated plugin instances and rejects editor or persistence commits', async () => {
    const { invoke, pluginStorage } = install();
    const snapshot = await invoke('plugin_instances_get', {
      pluginId: 'plugin',
    });
    expect(snapshot).toEqual({
      pluginId: 'plugin',
      instances: pluginStorage['plugin/instances'],
      modelRevision: 0,
      authorityGeneration: 0,
    });
    expect(
      await invoke('plugin_instances_get', { pluginId: 'missing' }),
    ).toMatchObject({ instances: [] });
    for (const command of [
      'editor_commit',
      'plugin_authority_reset',
      'plugin_instances_commit',
    ]) {
      await expect(invoke(command, { request: {} })).rejects.toThrow(
        'Unsupported command in frozen export',
      );
    }
    expect(
      await invoke('plugin_instances_get', { pluginId: 'plugin' }),
    ).toEqual(snapshot);
  });
});

describe('frozen key counts', () => {
  function visibilitySnapshot(master?: boolean) {
    return {
      settings: {
        language: 'ko',
        noteSettings: { frameLimit: 60 },
        ...(master === undefined ? {} : { keyCounterEnabled: master }),
      },
      positions: {
        hand: [
          {
            id: 'visible',
            counter: { enabled: true, fontSize: 29 },
            ghost: {
              enabled: true,
              settings: { counter: { enabled: true, fontSize: 19 } },
            },
          },
          { id: 'hidden', counter: { enabled: false } },
        ],
        foot: [{ id: 'default-counter' }],
      },
      statPositions: { hand: [{ counter: { enabled: true } }] },
      keyCounters: { hand: { A: 12 } },
    };
  }

  it.each([false, undefined])(
    'preserves hidden legacy counters for master=%s without changing the recording',
    async (master) => {
      const snapshot = visibilitySnapshot(master);
      const original = structuredClone(snapshot);
      const shim = installFrozenShim(snapshot, 'hand', {}, {}, [], 'live');
      dispose = shim.dispose;
      const invoke = frozenInvoke();
      const hiddenPositions = {
        hand: [
          {
            counter: { enabled: false, fontSize: 29 },
            ghost: { settings: { counter: { enabled: false, fontSize: 19 } } },
          },
          { counter: { enabled: false } },
        ],
        foot: [{ counter: { enabled: false } }],
      };
      expect(await invoke('app_bootstrap')).toMatchObject({
        settings: { keyCounterEnabled: true },
        positions: hiddenPositions,
        statPositions: snapshot.statPositions,
        keyCounters: snapshot.keyCounters,
      });
      expect(await invoke('editor_get')).toMatchObject({
        document: { keyPositions: hiddenPositions },
      });
      expect(snapshot).toEqual(original);
      expect(shim.counterEnabled).toBe(true);
    },
  );

  it('turns a recording with Raining off for every key into per-key settings', async () => {
    const snapshot = {
      settings: {
        language: 'ko',
        noteSettings: { frameLimit: 60 },
        keyCounterEnabled: true,
        noteEffect: false,
      },
      positions: {
        hand: [
          {
            id: 'with-ghost',
            noteEffectEnabled: true,
            ghost: { enabled: true, settings: { noteEffectEnabled: true } },
          },
          { id: 'plain' },
        ],
      },
    };
    const original = structuredClone(snapshot);
    const shim = installFrozenShim(snapshot, 'hand');
    dispose = shim.dispose;
    expect(await frozenInvoke()('app_bootstrap')).toMatchObject({
      settings: { noteEffect: true },
      positions: {
        hand: [
          {
            noteEffectEnabled: false,
            ghost: { settings: { noteEffectEnabled: false } },
          },
          { noteEffectEnabled: false },
        ],
      },
    });
    // The recording itself is not changed.
    expect(snapshot).toEqual(original);
  });

  it('keeps current per-key visibility when the compatibility field is true', async () => {
    const snapshot = visibilitySnapshot(true);
    const shim = installFrozenShim(snapshot, 'hand');
    dispose = shim.dispose;
    expect(await frozenInvoke()('positions_get')).toEqual(snapshot.positions);
  });

  function counts(counterBaseline?: Parameters<typeof installFrozenShim>[5]) {
    const snapshot = {
      settings: { language: 'ko', noteSettings: { frameLimit: 60 } },
      keys: { hand: [] },
      positions: { hand: [] },
      keyCounters: { hand: { A: 5, B: 7 } },
    };
    const shim = installFrozenShim(
      snapshot,
      'hand',
      {},
      {},
      [],
      counterBaseline,
    );
    dispose = shim.dispose;
    return shim.counters;
  }

  it('counts from zero, the live totals, or a run recorded counts', () => {
    expect(counts(undefined)).toEqual({});
    expect(counts('live')).toEqual({ hand: { A: 5, B: 7 } });
    expect(
      counts({
        mode: 'recorded',
        counters: { hand: { A: 40 }, foot: { F: 2 } },
      }),
    ).toEqual({ hand: { A: 40, B: 7 }, foot: { F: 2 } });
  });

  it('keeps the live totals for counts that are not whole presses', () => {
    expect(
      counts({ mode: 'recorded', counters: { hand: { A: -1, B: 1.5 } } }),
    ).toEqual({ hand: { A: 5, B: 7 } });
    expect(counts({ mode: 'recorded', counters: null })).toEqual({
      hand: { A: 5, B: 7 },
    });
  });
});
