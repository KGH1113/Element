import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { NativeKeyMappings } from '@src/types/editor';
import {
  createPluginWriteGateway,
  normalizePluginMappings,
} from './pluginWriteGateway';

const commitNativeChanges = vi.hoisted(() => vi.fn());
vi.mock('@api/modules/editorApi', () => ({
  editorApi: { commitNativeChanges, commit: vi.fn() },
}));

beforeEach(() => {
  vi.clearAllMocks();
});

describe('plugin native key writes', () => {
  it('normalizes compound bindings without interpreting a legacy label as a chord', () => {
    expect(
      normalizePluginMappings({
        hand: [
          'A+B',
          { keys: ['A', 'A', 'B', '', 'C|D'], match: 'all' },
          { keys: ['C'], match: 'any' },
          null,
        ],
      }),
    ).toEqual({ hand: ['A+B', { keys: ['A', 'B'], match: 'all' }, 'C', ''] });
    expect(() => normalizePluginMappings({ hand: 'A' })).toThrow(
      'must be an array',
    );
    expect(() => normalizePluginMappings(undefined)).toThrow(
      'JSON-serializable',
    );
  });

  it('commits key bindings and positions atomically with explicit multi-key opt-in', async () => {
    const active = () => true;
    const gateway = createPluginWriteGateway(active);
    const keys: NativeKeyMappings = {
      hand: [{ keys: ['A', 'B'], match: 'all' }],
    };
    const positions = { hand: [{ id: 'first', dx: 4 }] };
    commitNativeChanges.mockResolvedValue({ keys, keyPositions: positions });
    await expect(
      gateway.updateWithPositions(keys, positions, { multiKey: true }),
    ).resolves.toEqual({ keys, positions });
    expect(commitNativeChanges).toHaveBeenCalledWith(
      { keys, keyPositions: positions },
      true,
      active,
    );
    await gateway.update(keys);
    expect(commitNativeChanges).toHaveBeenLastCalledWith(
      { keys },
      false,
      active,
    );
  });

  it('rejects a disposed plugin before starting any native transaction', async () => {
    const gateway = createPluginWriteGateway(() => false);
    await expect(gateway.update({ hand: ['A'] })).rejects.toThrow(
      'no longer active',
    );
    expect(commitNativeChanges).not.toHaveBeenCalled();
  });
});
