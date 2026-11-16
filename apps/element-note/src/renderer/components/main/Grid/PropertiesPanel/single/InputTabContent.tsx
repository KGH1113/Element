import type { KeyPosition } from '@src/types/key/keys';
import KeySoundControls from '../KeySoundControls';

interface InputTabContentProps {
  keyIndex: number;
  keyPosition: KeyPosition;
  onKeyUpdate: (data: Partial<KeyPosition> & { index: number }) => void;
  onKeyPreview?: (index: number, updates: Partial<KeyPosition>) => void;
  panelElement?: HTMLElement | null;
  t: (key: string) => string | undefined;
}

/**
 * What a key does when it is pressed, besides lighting up: its sound. The
 * keyboard key itself is bound in its row on the element's page.
 */
export default function InputTabContent({
  keyIndex,
  keyPosition,
  onKeyUpdate,
  onKeyPreview,
  panelElement,
  t,
}: InputTabContentProps) {
  return (
    <KeySoundControls
      value={keyPosition}
      onChange={(property, value) => {
        const update = { [property]: value };
        onKeyPreview?.(keyIndex, update);
        onKeyUpdate({ index: keyIndex, ...update });
      }}
      panelElement={panelElement}
      t={t}
    />
  );
}
