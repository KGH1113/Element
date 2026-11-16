import type {
  NoteSettings,
  TabNoteSettings,
} from '@src/types/settings/noteSettings';

/**
 * What differs from the values every key viewer uses. A key viewer keeps only
 * these, so the rest keeps following the shared values.
 */
export const ownRainingValues = (
  values: NoteSettings,
  shared: NoteSettings,
): TabNoteSettings | null => {
  const own: Record<string, unknown> = {};
  (Object.keys(values) as (keyof NoteSettings)[]).forEach((key) => {
    if (values[key] !== shared[key]) own[key] = values[key];
  });
  return Object.keys(own).length > 0 ? (own as TabNoteSettings) : null;
};
