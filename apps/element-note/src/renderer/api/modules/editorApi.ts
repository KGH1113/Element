import { invoke, trackEditorSave, trackEditorInvocation } from '../invoke';
import { subscribe } from './shared';
import type {
  EditorAPI,
  EditorChanges,
  EditorCommitResult,
  EditorSnapshot,
  NativeEditorDocument,
  NativeElementPositions,
  NativeKeyMappings,
  NativeKeySlot,
  PluginEditorCommitRequest,
  EditorCommittedV1,
} from '@src/types/editor';

function canonicalSlot(slot: NativeKeySlot): string {
  return typeof slot === 'string'
    ? slot
    : slot.keys.join(slot.match === 'all' ? '+' : '|');
}

export function projectNativeKeyMappings(
  mappings: NativeKeyMappings,
): NonNullable<EditorChanges['keys']> {
  return Object.fromEntries(
    Object.entries(mappings).map(([mode, slots]) => [
      mode,
      slots.map(canonicalSlot),
    ]),
  );
}

function preserveNativeMappings(
  mappings: NonNullable<EditorChanges['keys']>,
  document: NativeEditorDocument,
): NativeKeyMappings {
  return Object.fromEntries(
    Object.entries(mappings).map(([mode, keys]) => {
      const existing = new Map(
        (document.keys[mode] ?? []).map((slot) => [canonicalSlot(slot), slot]),
      );
      return [mode, keys.map((key) => existing.get(key) ?? key)];
    }),
  );
}

function preserveNativePositions(
  positions: NativeElementPositions,
  existing: NativeElementPositions,
): NativeElementPositions {
  return Object.fromEntries(
    Object.entries(positions).map(([mode, elements]) => {
      const byId = new Map(
        (existing[mode] ?? [])
          .filter((element) => element.id)
          .map((element) => [element.id, element]),
      );
      return [
        mode,
        elements.map((element) => {
          const original = element.id ? byId.get(element.id) : undefined;
          if (!original) return element;
          return { ...original, ...element };
        }),
      ];
    }),
  );
}

/** v1 편집기로 표시하지 않는 v2 속성과 복합 키를 편집 과정에서 지우지 않는다. */
export function prepareEditorChanges(
  changes: EditorChanges,
  document: NativeEditorDocument,
): Partial<NativeEditorDocument> {
  const patch: Partial<NativeEditorDocument> = { schemaVersion: 1 };
  if (changes.keys !== undefined) {
    patch.keys = preserveNativeMappings(changes.keys, document);
  }
  for (const field of [
    'keyPositions',
    'statPositions',
    'graphPositions',
    'knobPositions',
    'spritePositions',
  ] as const) {
    if (changes[field] !== undefined) {
      patch[field] = preserveNativePositions(changes[field], document[field]);
    }
  }
  if (changes.layerGroups !== undefined) {
    patch.layerGroups = changes.layerGroups;
  }
  return patch;
}

type Invoke = typeof invoke;

const pluginCommitKeys = new Set([
  'baseRevision',
  'mutationId',
  'changes',
  'gestureId',
  'gestureIds',
  'multiKey',
]);

export function snapshotPluginEditorRequest(
  request: unknown,
): PluginEditorCommitRequest {
  const assertKeys = (value: unknown) => {
    if (!value || typeof value !== 'object' || Array.isArray(value)) {
      throw new TypeError('editor.commit request must be a JSON object');
    }
    const unsupported = Object.keys(value).find(
      (key) => !pluginCommitKeys.has(key),
    );
    if (unsupported !== undefined) {
      throw new TypeError(
        `editor.commit request contains unsupported key '${unsupported}'`,
      );
    }
  };
  assertKeys(request);
  let wire: unknown;
  try {
    wire = JSON.parse(JSON.stringify(request));
  } catch {
    throw new TypeError('editor.commit request must be JSON-serializable');
  }
  assertKeys(wire);
  const frozen = wire as PluginEditorCommitRequest;
  if (frozen.changes?.schemaVersion !== 1) {
    throw new TypeError('editor.commit changes.schemaVersion must be 1');
  }
  return frozen;
}

export function createEditorApi(invokeCommand: Invoke = invoke) {
  let pending: Promise<unknown> = Promise.resolve();
  const get = () => invokeCommand<EditorSnapshot>('editor_get');

  const serialize = <T>(run: () => Promise<T>): Promise<T> => {
    const result = pending.then(run);
    pending = result.catch(() => undefined);
    return result;
  };
  const commitRaw = (request: PluginEditorCommitRequest) =>
    invokeCommand<EditorCommitResult>('editor_commit', { request });
  const saveOwners = (changes: Partial<NativeEditorDocument>) =>
    Object.keys(changes)
      .filter(
        (field) =>
          field !== 'schemaVersion' &&
          changes[field as keyof NativeEditorDocument] !== undefined,
      )
      .map((field) => `editor:${field}`);

  return {
    get,
    async commit(
      request: PluginEditorCommitRequest,
      isActive: () => boolean = () => true,
    ) {
      // Preserve the plugin's explicit revision, mutation ID and multiKey opt-in.
      const frozen = snapshotPluginEditorRequest(request);
      return trackEditorInvocation(
        serialize(() => {
          if (!isActive()) throw new Error('Plugin is no longer active');
          return trackEditorSave(commitRaw(frozen), saveOwners(frozen.changes));
        }),
      );
    },
    onCommitted(listener: (event: EditorCommittedV1) => void) {
      return subscribe<EditorCommittedV1>('editor:committed', listener);
    },
    // Plugin compatibility writes are isolated from the UI's canonical-string
    // projection. A false multiKey opt-in reaches the native rejection gate.
    commitNativeChanges(
      changes: Partial<NativeEditorDocument>,
      multiKey = false,
      isActive: () => boolean = () => true,
    ) {
      const frozen = structuredClone(changes);
      return trackEditorInvocation(
        serialize(async () => {
          if (!isActive()) throw new Error('Plugin is no longer active');
          const snapshot = await get();
          if (!isActive()) throw new Error('Plugin is no longer active');
          await trackEditorSave(
            commitRaw({
              baseRevision: snapshot.revision,
              mutationId: crypto.randomUUID(),
              multiKey,
              changes: { ...frozen, schemaVersion: 1 },
            }),
            saveOwners(frozen),
          );
          return (await get()).document;
        }),
      );
    },
    commitChanges(changes: Parameters<EditorAPI['commitChanges']>[0]) {
      // 대기 중에 UI가 다음 드래그/편집을 시작해도 이번 변경은 고정한다.
      const frozenChanges = structuredClone(changes);
      const commit = serialize(async () => {
        const snapshot = await get();
        return invokeCommand<EditorCommitResult>('editor_commit', {
          request: {
            baseRevision: snapshot.revision,
            mutationId: crypto.randomUUID(),
            multiKey: true,
            changes: prepareEditorChanges(frozenChanges, snapshot.document),
          },
        });
      });
      // 실패는 호출자에게 전달하되 다음 편집의 실행을 막지 않는다.
      return trackEditorSave(
        commit,
        Object.keys(frozenChanges)
          .filter(
            (field) =>
              frozenChanges[field as keyof EditorChanges] !== undefined,
          )
          .map((field) => `editor:${field}`),
      );
    },
  };
}

export const editorApi = createEditorApi();
