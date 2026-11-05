import {
  invoke,
  trackEditorInvocation,
  clearEditorSaveFailure,
  setEditorSaveFailure,
} from '@api/invoke';

export interface SavedPluginInstance {
  instanceId: string;
  position: { x: number; y: number };
  settings?: Record<string, unknown>;
  measuredSize?: { width: number; height: number };
  tabId?: string;
  hidden?: boolean;
  zIndex?: number;
  groupId?: string;
}

interface InstanceSnapshot {
  instances: SavedPluginInstance[];
  modelRevision: number;
  authorityGeneration: number;
}

type Invoke = typeof invoke;

/** 모든 플러그인이 공유하는 v2 모델 revision과 쓰기 순서. */
export function createInstancePersistenceRuntime(
  invokeCommand: Invoke = invoke,
) {
  let authority: Promise<{ authorityGeneration: number }> | undefined;
  let pending: Promise<unknown> = Promise.resolve();

  function ensureAuthority() {
    authority ??= invokeCommand<{ authorityGeneration: number }>(
      'plugin_authority_reset',
    ).catch((error: unknown) => {
      authority = undefined;
      throw error;
    });
    return authority;
  }

  return {
    forPlugin(pluginId: string) {
      let active = true;
      const failureOwner = `plugin:${pluginId}`;
      const ownMutations = new Set<string>();
      const readSnapshot = () =>
        invokeCommand<InstanceSnapshot>('plugin_instances_get', { pluginId });

      return {
        load: () => pending.then(readSnapshot),
        dispose: () => {
          active = false;
        },
        isOwnMutation: (id?: string) => Boolean(id && ownMutations.has(id)),
        save(instances: SavedPluginInstance[]) {
          const frozen = structuredClone(instances);
          const saving = pending
            .then(async () => {
              if (!active) return;
              const lease = await ensureAuthority();
              const snapshot = await readSnapshot();
              if (!active) return;
              const mutationId = crypto.randomUUID();
              ownMutations.add(mutationId);
              if (ownMutations.size > 64) {
                ownMutations.delete(ownMutations.values().next().value!);
              }
              await invokeCommand('plugin_instances_commit', {
                request: {
                  pluginId,
                  instances: frozen,
                  mutationId,
                  authorityGeneration: lease.authorityGeneration,
                  expectedModelRevision: snapshot.modelRevision,
                },
              });
              clearEditorSaveFailure(failureOwner);
            })
            .catch((error: unknown) => {
              setEditorSaveFailure(failureOwner, error);
              throw error;
            });
          pending = saving.catch(() => undefined);
          return trackEditorInvocation(saving);
        },
      };
    },
  };
}

export const instancePersistence = createInstancePersistenceRuntime();
