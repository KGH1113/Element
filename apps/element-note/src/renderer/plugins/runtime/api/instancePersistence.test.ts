import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { invoke } from '@api/invoke';
import { clearEditorSaveFailure, setEditorSaveFailure } from '@api/invoke';
import {
  createInstancePersistenceRuntime,
  type SavedPluginInstance,
} from './instancePersistence';

vi.mock('@api/invoke', () => ({
  invoke: vi.fn(),
  trackEditorInvocation: <T>(request: Promise<T>) => request,
  clearEditorSaveFailure: vi.fn(),
  setEditorSaveFailure: vi.fn(),
}));

const instance = (): SavedPluginInstance => ({
  instanceId: '11111111-1111-4111-8111-111111111111',
  position: { x: 10, y: 20 },
  tabId: 'foot-2k',
  hidden: true,
  zIndex: 3,
  settings: { title: 'Foot', enabled: true },
});

describe('v2 plugin instance persistence', () => {
  let revision = 0;
  const invokeCommand = vi.fn();
  beforeEach(() => {
    vi.mocked(clearEditorSaveFailure).mockClear();
    vi.mocked(setEditorSaveFailure).mockClear();
    revision = 0;
    invokeCommand.mockReset().mockImplementation(async (command, args) => {
      if (command === 'plugin_authority_reset') {
        return { authorityGeneration: 7 };
      }
      if (command === 'plugin_instances_get') {
        return {
          instances: [],
          modelRevision: revision,
          authorityGeneration: 7,
        };
      }
      if (command === 'plugin_instances_commit') {
        expect(args.request.expectedModelRevision).toBe(revision);
        revision += 1;
        return { modelRevision: revision };
      }
      throw new Error(`Unexpected command: ${command}`);
    });
  });

  const runtime = () =>
    createInstancePersistenceRuntime(invokeCommand as typeof invoke);

  it('serializes different plugins against one model and keeps stable fields', async () => {
    const shared = runtime();
    const first = shared.forPlugin('plugin-first');
    const second = shared.forPlugin('plugin-second');
    const saved = instance();
    await Promise.all([first.save([saved]), second.save([saved])]);
    const commits = invokeCommand.mock.calls.filter(
      ([command]) => command === 'plugin_instances_commit',
    );
    expect(
      commits.map(([, args]) => args.request.expectedModelRevision),
    ).toEqual([0, 1]);
    expect(commits[0][1].request).toMatchObject({
      pluginId: 'plugin-first',
      authorityGeneration: 7,
      instances: [saved],
    });
    expect(first.isOwnMutation(commits[0][1].request.mutationId)).toBe(true);
    expect(second.isOwnMutation(commits[0][1].request.mutationId)).toBe(false);
    expect(
      invokeCommand.mock.calls.filter(
        ([command]) => command === 'plugin_authority_reset',
      ),
    ).toHaveLength(1);
  });

  it('freezes a queued edit before the UI changes again', async () => {
    const client = runtime().forPlugin('plugin-first');
    const data = instance();
    const saving = client.save([data]);
    data.position.x = 999;
    await saving;
    expect(
      invokeCommand.mock.calls.find(
        ([command]) => command === 'plugin_instances_commit',
      )?.[1].request.instances[0].position.x,
    ).toBe(10);
  });

  it('does not submit writes from a disposed plugin runtime', async () => {
    const client = runtime().forPlugin('plugin-first');
    const saving = client.save([instance()]);
    client.dispose();
    await saving;
    expect(invokeCommand).not.toHaveBeenCalled();
  });

  it('reports a rejected write and allows the following edit to retry', async () => {
    const client = runtime().forPlugin('plugin-first');
    const original = invokeCommand.getMockImplementation()!;
    let failed = false;
    invokeCommand.mockImplementation(async (command, args) => {
      if (command === 'plugin_instances_commit' && !failed) {
        failed = true;
        throw new Error('disk unavailable');
      }
      return original(command, args);
    });
    await expect(client.save([instance()])).rejects.toThrow('disk unavailable');
    await expect(client.save([instance()])).resolves.toBeUndefined();
    expect(revision).toBe(1);
  });

  it('waits for an already-dispatched old write before loading a reloaded plugin', async () => {
    const shared = runtime();
    const previous = shared.forPlugin('plugin-first');
    const original = invokeCommand.getMockImplementation()!;
    let releaseCommit: (() => void) | undefined;
    invokeCommand.mockImplementation(async (command, args) => {
      if (command === 'plugin_instances_commit') {
        await new Promise<void>((resolve) => {
          releaseCommit = resolve;
        });
      }
      return original(command, args);
    });
    const saving = previous.save([instance()]);
    await vi.waitFor(() => expect(releaseCommit).toBeDefined());
    previous.dispose();
    const reloaded = shared.forPlugin('plugin-first');
    const loading = reloaded.load();
    await Promise.resolve();
    expect(
      invokeCommand.mock.calls.filter(
        ([command]) => command === 'plugin_instances_get',
      ),
    ).toHaveLength(1);
    releaseCommit!();
    await saving;
    await expect(loading).resolves.toMatchObject({ modelRevision: 1 });
  });

  it('does not clear an unsaved failure when a disposed queued save is skipped', async () => {
    const client = runtime().forPlugin('plugin-first');
    const original = invokeCommand.getMockImplementation()!;
    const failure = new Error('disk unavailable');
    invokeCommand.mockImplementation(async (command, args) => {
      if (command === 'plugin_instances_commit') throw failure;
      return original(command, args);
    });
    await expect(client.save([instance()])).rejects.toBe(failure);
    expect(setEditorSaveFailure).toHaveBeenCalledWith(
      'plugin:plugin-first',
      failure,
    );
    const skipped = client.save([instance()]);
    client.dispose();
    await skipped;
    expect(clearEditorSaveFailure).not.toHaveBeenCalled();
  });
});
