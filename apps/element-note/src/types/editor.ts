import type { KeyMappings, KeyPositions } from './key/keys';
import type { StatItemPositions } from './key/statItems';
import type { GraphItemPositions } from './key/graphItems';
import type { KnobItemPositions } from './key/knobs';
import type { LayerGroups } from './layerGroups';
import type { ReactiveSpritePosition, SpritePositions } from './key/sprites';
import type { ReadyUnsubscribe } from './plugin/api';

export type CanonicalReactiveSpritePosition = ReactiveSpritePosition & {
  id: string;
};
export type CanonicalSpritePositions = Record<
  string,
  CanonicalReactiveSpritePosition[]
>;

/** DmNote 2의 저장 형식. 기존 키뷰어에는 canonical 문자열을 표시한다. */
export type NativeKeySlot = string | { keys: string[]; match: 'all' | 'any' };

export type NativeKeyMappings = Record<string, NativeKeySlot[]>;
export type NativeElement = Record<string, unknown> & { id?: string };
export type NativeElementPositions = Record<string, NativeElement[]>;

export interface NativeEditorDocument {
  schemaVersion: 1;
  keys: NativeKeyMappings;
  keyPositions: NativeElementPositions;
  statPositions: NativeElementPositions;
  graphPositions: NativeElementPositions;
  knobPositions: NativeElementPositions;
  spritePositions: NativeElementPositions;
  layerGroups: LayerGroups;
}

export interface EditorSnapshot {
  revision: number;
  document: NativeEditorDocument;
}

/** 기존 편집기에서 바꿀 수 있는 필드만 전달하며 나머지 v2 데이터는 보존한다. */
export interface EditorChanges {
  keys?: KeyMappings;
  keyPositions?: KeyPositions;
  statPositions?: StatItemPositions;
  graphPositions?: GraphItemPositions;
  knobPositions?: KnobItemPositions;
  layerGroups?: LayerGroups;
  spritePositions?: SpritePositions;
}

export interface EditorCommitResult {
  revision: number;
  changedFields: (keyof Omit<NativeEditorDocument, 'schemaVersion'>)[];
}

export interface PluginEditorCommitRequest {
  baseRevision: number;
  mutationId: string;
  changes: Partial<NativeEditorDocument> & { schemaVersion: 1 };
  multiKey?: boolean;
  gestureId?: string;
  gestureIds?: string[];
}

export interface EditorCommittedV1 {
  schemaVersion: 1;
  revision: number;
  mutationId: string;
  origin?: string;
  changedFields: EditorCommitResult['changedFields'];
  patch: Partial<NativeEditorDocument> & { schemaVersion: 1 };
  gestureId?: string | null;
  gestureIds?: string[];
}

export interface EditorAPI {
  get(): Promise<EditorSnapshot>;
  commit(request: PluginEditorCommitRequest): Promise<EditorCommitResult>;
  onCommitted(listener: (event: EditorCommittedV1) => void): ReadyUnsubscribe;
  commitChanges(changes: EditorChanges): Promise<EditorCommitResult>;
}
