import { useTranslation } from '@contexts/useTranslation';
import { useSettingsStore, type GridSettings } from '@stores/useSettingsStore';
import { usePalette } from '@hooks/Modal/usePalette';
import {
  SettingRow,
  SettingToggleRow,
} from '@components/main/common/SettingRow';
import { NumberInput } from '@components/main/common/NumberInput';
import Palette from '@components/main/Modal/content/pickers/Palette';

/**
 * Settings that apply to every key and every tab, moved here from the editor:
 * the editor's sidebar is only about the elements of the open tab.
 */

const useGridSetting = () => {
  const gridSettings = useSettingsStore((state) => state.gridSettings);
  const setGridSettings = useSettingsStore((state) => state.setGridSettings);
  const change = async (key: keyof GridSettings, value: boolean | number) => {
    const next: GridSettings = { ...gridSettings, [key]: value };
    setGridSettings(next);
    try {
      await window.api.settings.update({ gridSettings: next });
    } catch (error) {
      console.error('Failed to update grid settings', error);
    }
  };
  return { gridSettings, change };
};

/** How the key viewer looks as a whole: background and padding. */
export function ViewerLookGroup() {
  const { t } = useTranslation();
  const { color, handleColorChange } = usePalette();
  const { gridSettings, change } = useGridSetting();

  return (
    <section className="en-settings-group">
      <SettingRow
        label={t('settingsWorkspace.background')}
        description={t('settingsWorkspace.backgroundHelp')}
      >
        <div className="en-settings-palette">
          <Palette
            color={color}
            onColorChange={(next) => {
              if (typeof next === 'string') handleColorChange(next);
            }}
          />
        </div>
      </SettingRow>
      <SettingRow
        label={t('gridSettings.overlayPadding')}
        description={t('settingsWorkspace.paddingHelp')}
      >
        <NumberInput
          value={gridSettings.overlayPadding}
          onChange={(value) => void change('overlayPadding', value)}
          min={0}
          max={30}
          suffix="px"
          width="64px"
          ariaLabel={t('gridSettings.overlayPadding')}
        />
      </SettingRow>
    </section>
  );
}

/** Helpers of the editing screen: grid, guides, minimap. */
export function EditorGroup() {
  const { t } = useTranslation();
  const { gridSettings, change } = useGridSetting();

  return (
    <>
      <section className="en-settings-group">
        <SettingRow
          label={t('gridSettings.gridSnapSize')}
          description={t('settingsWorkspace.snapHelp')}
        >
          <NumberInput
            value={gridSettings.gridSnapSize}
            onChange={(value) => void change('gridSnapSize', value)}
            min={1}
            max={10}
            suffix="px"
            width="64px"
            ariaLabel={t('gridSettings.gridSnapSize')}
          />
        </SettingRow>
        <SettingToggleRow
          label={t('gridSettings.minimapEnabled')}
          description={t('settingsWorkspace.minimapHelp')}
          checked={gridSettings.minimapEnabled}
          onToggle={() =>
            void change('minimapEnabled', !gridSettings.minimapEnabled)
          }
        />
      </section>
      <section className="en-settings-group">
        <SettingToggleRow
          label={t('gridSettings.alignmentGuides')}
          description={t('settingsWorkspace.alignHelp')}
          checked={gridSettings.alignmentGuides}
          onToggle={() =>
            void change('alignmentGuides', !gridSettings.alignmentGuides)
          }
        />
        <SettingToggleRow
          label={t('gridSettings.spacingGuides')}
          description={t('settingsWorkspace.spacingHelp')}
          checked={gridSettings.spacingGuides}
          onToggle={() =>
            void change('spacingGuides', !gridSettings.spacingGuides)
          }
        />
        <SettingToggleRow
          label={t('gridSettings.sizeMatchGuides')}
          description={t('settingsWorkspace.sizeHelp')}
          checked={gridSettings.sizeMatchGuides}
          onToggle={() =>
            void change('sizeMatchGuides', !gridSettings.sizeMatchGuides)
          }
        />
      </section>
    </>
  );
}
