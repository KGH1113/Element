import { editorApi } from '@api/modules/editorApi';
import type {
  NativeKeyMappings,
  NativeKeySlot,
  NativeElementPositions,
  PluginEditorCommitRequest,
} from '@src/types/editor';

// Match the native slot normalizer without treating a canonical label as input.
export function normalizePluginSlot(raw: unknown): NativeKeySlot {
  if (typeof raw === 'string') return raw;
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return '';
  const slot = raw as { keys?: unknown; match?: unknown };
  if (
    (slot.match !== 'all' && slot.match !== 'any') ||
    !Array.isArray(slot.keys)
  )
    return '';
  const keys = [
    ...new Set(
      slot.keys.filter(
        (key): key is string =>
          typeof key === 'string' &&
          key !== '' &&
          !key.includes('+') &&
          !key.includes('|'),
      ),
    ),
  ].slice(0, 8);
  return keys.length > 1 ? { keys, match: slot.match } : (keys[0] ?? '');
}

export function normalizePluginMappings(raw: unknown): NativeKeyMappings {
  let wire: unknown;
  try {
    wire = JSON.parse(JSON.stringify(raw));
  } catch {
    throw new TypeError('keys mappings must be JSON-serializable');
  }
  if (!wire || typeof wire !== 'object' || Array.isArray(wire))
    throw new TypeError('keys mappings must be an object of mode arrays');
  return Object.fromEntries(
    Object.entries(wire).map(([mode, slots]) => {
      if (!Array.isArray(slots))
        throw new TypeError(`keys mappings['${mode}'] must be an array`);
      return [mode, slots.map(normalizePluginSlot)];
    }),
  );
}

export function createPluginWriteGateway(isActive: () => boolean) {
  const assertActive = () => {
    if (!isActive()) throw new Error('Plugin is no longer active');
  };
  return {
    async update(
      mappings: NativeKeyMappings,
      options?: { multiKey?: boolean },
    ) {
      assertActive();
      return (
        await editorApi.commitNativeChanges(
          { keys: normalizePluginMappings(mappings) },
          options?.multiKey === true,
          isActive,
        )
      ).keys;
    },
    async updateWithPositions(
      mappings: NativeKeyMappings,
      positions: NativeElementPositions,
      options?: { multiKey?: boolean },
    ) {
      assertActive();
      const document = await editorApi.commitNativeChanges(
        { keys: normalizePluginMappings(mappings), keyPositions: positions },
        options?.multiKey === true,
        isActive,
      );
      return { keys: document.keys, positions: document.keyPositions };
    },
    async positions(
      field:
        'keyPositions' | 'statPositions' | 'graphPositions' | 'knobPositions',
      positions: NativeElementPositions,
    ) {
      assertActive();
      return (
        await editorApi.commitNativeChanges(
          { [field]: positions },
          false,
          isActive,
        )
      )[field];
    },
    async commit(request: PluginEditorCommitRequest) {
      assertActive();
      return editorApi.commit(request, isActive);
    },
  };
}
