import { invoke, trackEditorSave, trackEditorInvocation } from '../invoke';
import { historyStepId } from '../historyStep';
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

/** 화면 값으로 통째로 바꿔 저장하는 필드. 키 매핑은 복합 키를 지켜야 해서 commitChanges로 저장한다. */
export type EditorFieldValues = Omit<EditorChanges, 'keys'>;

/** 다른 저장이 먼저 끝나 리비전이 어긋났을 때 네이티브가 알려 주는 현재 리비전. */
function conflictRevision(error: unknown): number | null {
  const rejected = error as {
    errorCode?: unknown;
    details?: { currentRevision?: unknown };
  } | null;
  const current = rejected?.details?.currentRevision;
  return rejected?.errorCode === 'REVISION_CONFLICT' &&
    typeof current === 'number'
    ? current
    : null;
}

const saveOwners = (changes: object) =>
  Object.entries(changes)
    .filter(
      ([field, value]) => field !== 'schemaVersion' && value !== undefined,
    )
    .map(([field]) => `editor:${field}`);

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

export function createEditorApi(
  invokeCommand: Invoke = invoke,
  stepId: () => string | undefined = historyStepId,
) {
  let pending: Promise<unknown> = Promise.resolve();
  /** 이 창의 마지막 저장이 남긴 리비전. 모르면 null이고, 저장하기 전에 읽어 온다. */
  let savedRevision: number | null = null;
  const get = () => invokeCommand<EditorSnapshot>('editor_get');

  /**
   * 줄의 맨 끝에서 아직 보내지 않은 필드 저장. 같은 실행 취소 단계에서 같은 필드의 더 새 값이
   * 오면 이 저장의 값을 바꿔 한 번만 보낸다: 저장이 밀려도 미리보기가 뒤처지지 않는다.
   */
  let waiting: {
    gestureId: string;
    values: EditorFieldValues;
    commit: Promise<EditorCommitResult>;
  } | null = null;

  const serialize = <T>(run: () => Promise<T>): Promise<T> => {
    // 뒤에 다른 저장이 줄을 서면 순서가 바뀌지 않게 더는 합치지 않는다.
    waiting = null;
    const result = pending.then(run);
    pending = result.catch(() => undefined);
    return result;
  };
  const commitRaw = async (request: PluginEditorCommitRequest) => {
    try {
      const result = await invokeCommand<EditorCommitResult>('editor_commit', {
        request,
      });
      savedRevision = result.revision;
      return result;
    } catch (error) {
      savedRevision = conflictRevision(error);
      throw error;
    }
  };
  /** 화면의 저장은 사용자의 동작 하나가 실행 취소 한 단계가 되도록 단계 ID를 싣는다. */
  const inStep = (gestureId: string | undefined) =>
    gestureId ? { gestureId } : {};

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
      const gestureId = stepId();
      const commit = serialize(async () => {
        const snapshot = await get();
        return commitRaw({
          baseRevision: snapshot.revision,
          mutationId: crypto.randomUUID(),
          multiKey: true,
          ...inStep(gestureId),
          changes: prepareEditorChanges(
            frozenChanges,
            snapshot.document,
          ) as PluginEditorCommitRequest['changes'],
        });
      });
      // 실패는 호출자에게 전달하되 다음 편집의 실행을 막지 않는다.
      return trackEditorSave(commit, saveOwners(frozenChanges));
    },
    /**
     * 화면의 값으로 필드를 통째로 바꿔 저장한다. 문서를 읽지 않고 리비전만 맞추므로
     * 미리보기처럼 자주 저장해도 요청 하나로 끝난다.
     */
    replaceFields(values: EditorFieldValues) {
      const frozen = structuredClone(values);
      const gestureId = stepId();
      const owners = saveOwners(frozen);
      if (
        waiting &&
        waiting.gestureId === gestureId &&
        saveOwners(waiting.values).join() === owners.join()
      ) {
        Object.assign(waiting.values, frozen);
        return trackEditorSave(waiting.commit, owners);
      }
      const sending = { ...frozen };
      const commit = serialize(async () => {
        // 보내기 시작한 저장에는 더 싣지 않는다.
        if (waiting?.values === sending) waiting = null;
        for (let retried = false; ; retried = true) {
          const baseRevision = savedRevision ?? (await get()).revision;
          try {
            return await commitRaw({
              baseRevision,
              mutationId: crypto.randomUUID(),
              ...inStep(gestureId),
              changes: {
                ...sending,
                schemaVersion: 1,
              } as PluginEditorCommitRequest['changes'],
            });
          } catch (error) {
            // 실행 취소나 다른 창이 먼저 저장했다. 화면이 가장 새 값이므로 한 번 더 저장한다.
            if (retried || savedRevision === null) throw error;
          }
        }
      });
      if (gestureId) waiting = { gestureId, values: sending, commit };
      return trackEditorSave(commit, owners);
    },
  };
}

export const editorApi = createEditorApi();
