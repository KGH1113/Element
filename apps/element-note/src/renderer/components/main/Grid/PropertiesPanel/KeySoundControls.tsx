import { useRef, useState } from 'react';
import { Dimmed } from '@components/main/Sidebar/SidebarParts';
import type { KeyPosition } from '@src/types/key/keys';
import SoundPicker from '@components/main/Modal/content/pickers/SoundPicker';
import { NumberInput, PropertyRow } from './PropertyInputs';
import PropertySection from './PropertySection';

interface KeySoundControlsProps {
  value: Pick<KeyPosition, 'soundEnabled' | 'soundPath' | 'soundVolume'>;
  mixed?: { enabled?: boolean; path?: boolean; volume?: boolean };
  onChange: (
    property: 'soundPath' | 'soundVolume',
    value: string | number,
  ) => void;
  panelElement?: HTMLElement | null;
  t: (key: string) => string | undefined;
}

/**
 * The sound a key makes: its file and how loud it is. Whether the key makes a
 * sound at all is the part's switch, in the page head; while it is off these
 * stay in place, greyed out.
 */
export default function KeySoundControls(props: KeySoundControlsProps) {
  return (
    <PropertySection
      id="input-sound"
      title={props.t('propertiesPanel.sectionSound') || 'Sound'}
      defaultOpen
    >
      <KeySoundFields {...props} />
    </PropertySection>
  );
}

// Keeping the chooser's state inside the page closes it with the page.
function KeySoundFields({
  value,
  mixed,
  onChange,
  panelElement,
  t,
}: KeySoundControlsProps) {
  const buttonRef = useRef<HTMLButtonElement>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const canEdit = (value.soundEnabled ?? false) || !!mixed?.enabled;
  const fileName = value.soundPath?.split(/[\\/]/).pop();
  return (
    <Dimmed off={!canEdit}>
      <PropertyRow label={t('propertiesPanel.keySound') || 'Sound file'}>
        <button
          ref={buttonRef}
          type="button"
          aria-haspopup="dialog"
          aria-expanded={pickerOpen}
          onClick={() => setPickerOpen((open) => !open)}
          className="dmn-inline-action max-w-[160px]"
          title={fileName}
        >
          <span className="min-w-0 truncate">
            {mixed?.path
              ? t('propertiesPanel.mixedValues') || 'Different files'
              : fileName ||
                t('propertiesPanel.chooseSound') ||
                'Choose a sound'}
          </span>
        </button>
      </PropertyRow>
      <PropertyRow label={t('propertiesPanel.soundVolume') || 'Volume'}>
        <NumberInput
          visual="volume"
          value={value.soundVolume ?? 100}
          onChange={(next) =>
            onChange('soundVolume', Math.max(0, Math.min(200, next)))
          }
          min={0}
          max={200}
          suffix="%"
          isMixed={mixed?.volume}
          ariaLabel={t('propertiesPanel.soundVolume') || 'Volume'}
        />
      </PropertyRow>
      {canEdit && pickerOpen && (
        <SoundPicker
          open
          referenceRef={buttonRef}
          panelElement={panelElement}
          selectedSound={value.soundPath || null}
          onSoundSelect={(path) => onChange('soundPath', path || '')}
          onClose={() => setPickerOpen(false)}
          interactiveRefs={[buttonRef]}
          previewVolume={value.soundVolume ?? 100}
        />
      )}
    </Dimmed>
  );
}
