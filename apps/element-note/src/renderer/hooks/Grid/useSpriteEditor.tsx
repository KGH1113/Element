import { useState } from 'react';
import SpriteEditorModal from '@components/main/Modal/content/editors/SpriteEditorModal';
import { editorApi } from '@api/modules/editorApi';
import { reactiveSpritePositionSchema } from '@src/types/key/sprites';
import type {
  ReactiveSpritePosition,
  SpritePositions,
} from '@src/types/key/sprites';

export function updateSpriteCollection(
  positions: SpritePositions,
  mode: string,
  previousId: string | undefined,
  next: ReactiveSpritePosition | null,
): SpritePositions {
  const current = positions[mode] ?? [];
  if (previousId && !current.some((sprite) => sprite.id === previousId)) {
    throw new Error(
      'The sprite was removed. Close this editor and select it again.',
    );
  }
  const updated = previousId
    ? current.flatMap((sprite) =>
        sprite.id === previousId
          ? next
            ? [{ ...sprite, ...next, id: previousId }]
            : []
          : [sprite],
      )
    : next
      ? [...current, next]
      : current;
  return { ...positions, [mode]: updated };
}

export function useSpriteEditor() {
  const [editing, setEditing] = useState<{
    mode: string;
    initial: ReactiveSpritePosition | null;
    initialPosition?: { dx: number; dy: number };
  } | null>(null);

  const save = async (next: ReactiveSpritePosition | null) => {
    if (!editing) return;
    const snapshot = await editorApi.get();
    const currentPositions: SpritePositions = Object.fromEntries(
      Object.entries(snapshot.document.spritePositions).map(
        ([mode, sprites]) => [
          mode,
          sprites.map((sprite) => ({
            ...sprite,
            ...reactiveSpritePositionSchema.parse(sprite),
          })),
        ],
      ),
    );
    const spritePositions = updateSpriteCollection(
      currentPositions,
      editing.mode,
      editing.initial?.id,
      next,
    );
    await editorApi.commitChanges({ spritePositions });
    setEditing((current) => (current === editing ? null : current));
  };

  return {
    openCreate: (mode: string, initialPosition: { dx: number; dy: number }) =>
      setEditing({ mode, initial: null, initialPosition }),
    openEdit: (mode: string, initial: ReactiveSpritePosition) =>
      setEditing({ mode, initial: structuredClone(initial) }),
    modal: editing ? (
      <SpriteEditorModal
        key={`${editing.mode}-${editing.initial?.id ?? 'new'}`}
        {...editing}
        onSave={save}
        onDelete={editing.initial ? () => save(null) : undefined}
        onClose={() => setEditing(null)}
      />
    ) : null,
  };
}
