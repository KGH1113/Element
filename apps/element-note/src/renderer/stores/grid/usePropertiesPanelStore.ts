import { create } from 'zustand';
import type { PluginSettingsDefinition } from '@src/types/plugin/api';

export type PluginSettingsPanelPayload = {
  pluginId: string;
  definition: PluginSettingsDefinition;
  settings: Record<string, unknown>;
  originalSettings: Record<string, unknown>;
  onChange: (nextSettings: Record<string, unknown>) => void;
  onConfirm: (
    nextSettings: Record<string, unknown>,
    originalSettings: Record<string, unknown>,
  ) => void | Promise<void>;
  onCancel: (originalSettings: Record<string, unknown>) => void;
  resolve: (confirmed: boolean) => void;
};

interface PropertiesPanelState {
  pluginSettingsPanel: PluginSettingsPanelPayload | null;
  openPluginSettingsPanel: (payload: PluginSettingsPanelPayload) => void;
  closePluginSettingsPanel: () => void;
}

export const usePropertiesPanelStore = create<PropertiesPanelState>(
  (set, get) => ({
    pluginSettingsPanel: null,
    openPluginSettingsPanel: (payload) => {
      const existing = get().pluginSettingsPanel;
      if (existing) {
        try {
          existing.onCancel(existing.originalSettings);
        } catch (error) {
          console.error('[Plugin Settings] Failed to cancel settings:', error);
        } finally {
          existing.resolve(false);
        }
      }
      set({ pluginSettingsPanel: payload });
    },
    closePluginSettingsPanel: () => set({ pluginSettingsPanel: null }),
  }),
);
