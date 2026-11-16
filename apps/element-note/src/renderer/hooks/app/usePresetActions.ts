import { useTranslation } from '@contexts/useTranslation';
import { useHistoryStore } from '@stores/data/useHistoryStore';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useLayerGroupStore } from '@stores/data/useLayerGroupStore';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import { getCounterCacheSnapshot } from '@stores/signals/keyCounterCache';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useFontStore } from '@stores/useFontStore';

/** What a preset load replaces, kept so the load can be undone. */
const captureBeforeLoad = async () => {
  const keyState = useKeyStore.getState();
  const keyCounters = await window.api.keys
    .getCounters()
    .catch(() => getCounterCacheSnapshot());
  const settings = useSettingsStore.getState();
  const fontState = useFontStore.getState();

  return {
    keyMappings: keyState.keyMappings,
    positions: keyState.positions,
    statPositions: useStatItemStore.getState().positions,
    graphPositions: useGraphItemStore.getState().positions,
    pluginElements: usePluginDisplayElementStore.getState().elements,
    layerGroups: useLayerGroupStore.getState().layerGroups,
    keyCounters,
    tabs: keyState.tabs,
    selectedViewerTabs: keyState.selectedViewerTabs,
    selectedKeyType: keyState.selectedKeyType,
    settingsSnapshot: {
      useCustomCSS: settings.useCustomCSS,
      customCSSContent: settings.customCSSContent,
      customCSSPath: settings.customCSSPath,
      useCustomJS: settings.useCustomJS,
      jsPlugins: settings.jsPlugins,
      fontSettings: { customFonts: fontState.customFonts },
      backgroundColor: settings.backgroundColor,
      noteSettings: settings.noteSettings,
      noteEffect: settings.noteEffect,
      tabNoteOverrides: settings.tabNoteOverrides,
    },
  };
};

const errorCode = (error: unknown): string => {
  if (typeof error === 'string') return error;
  if (error && typeof error === 'object') {
    const message = (error as { message?: unknown }).message;
    if (typeof message === 'string') return message;
  }
  return '';
};

/** Saving and loading presets, for every key viewer or for one of them. */
export function usePresetActions(showAlert: (message: string) => void) {
  const { t } = useTranslation();
  const pushHistoryState = useHistoryStore((state) => state.pushState);

  const saveAll = async () => {
    try {
      const result = await window.api.presets.save();
      showAlert(
        result?.success ? t('preset.saveSuccess') : t('preset.saveFail'),
      );
    } catch (error) {
      console.error('Failed to save preset', error);
      showAlert(t('preset.saveFail'));
    }
  };

  const loadAll = async () => {
    try {
      const before = await captureBeforeLoad();
      const result = await window.api.presets.load();
      if (result?.success) pushHistoryState(before);
      showAlert(
        result?.success ? t('preset.loadSuccess') : t('preset.loadFail'),
      );
    } catch (error) {
      console.error('Failed to load preset', error);
      showAlert(t('preset.loadFail'));
    }
  };

  const saveTab = async (tabId?: string) => {
    try {
      const result = await window.api.presets.saveTab(tabId);
      showAlert(
        result?.success ? t('preset.saveTabSuccess') : t('preset.saveTabFail'),
      );
    } catch (error) {
      console.error('Failed to save tab preset', error);
      showAlert(t('preset.saveTabFail'));
    }
  };

  const loadTab = async (tabId?: string) => {
    try {
      const before = await captureBeforeLoad();
      const result = await window.api.presets.loadTab(tabId);
      if (result?.success) pushHistoryState(before);
      showAlert(
        result?.success ? t('preset.loadTabSuccess') : t('preset.loadTabFail'),
      );
    } catch (error) {
      console.error('Failed to load tab preset', error);
      const code = errorCode(error);
      showAlert(
        code.includes('tab-preset-ambiguous-source')
          ? t('preset.loadTabAmbiguousSource')
          : code.includes('invalid-tab-preset') ||
              code.includes('invalid-preset')
            ? t('preset.loadTabInvalidPreset')
            : t('preset.loadTabFail'),
      );
    }
  };

  return { saveAll, loadAll, saveTab, loadTab };
}
