import { SettingsState } from '@src/types/settings/settings';
import {
  KeyViewerTab,
  SelectedViewerTabs,
  KeyMappings,
  KeyPositions,
  KeyCounters,
} from '@src/types/key/keys';
import type { StatItemPositions } from '@src/types/key/statItems';
import type { GraphItemPositions } from '@src/types/key/graphItems';
import type { KnobItemPositions } from '@src/types/key/knobs';
import type { DefaultsPayload } from '@src/renderer/defaults';
import type { LayerGroups } from '@src/types/layerGroups';
import type { TabNoteOverrides } from '@src/types/settings/noteSettings';
import type { TabCssOverrides } from '@src/types/plugin/css';

/** How the app was started; Element can open it with ADOFAI. */
export interface LaunchContext {
  launchedByGame: boolean;
}

export interface BootstrapPayload {
  settings: SettingsState;
  defaults: DefaultsPayload;
  keys: KeyMappings;
  positions: KeyPositions;
  statPositions: StatItemPositions;
  graphPositions: GraphItemPositions;
  knobPositions: KnobItemPositions;
  tabs: KeyViewerTab[];
  selectedViewerTabs: SelectedViewerTabs;
  selectedKeyType: string;
  currentMode: string;
  overlay: {
    visible: boolean;
    locked: boolean;
    anchor: string;
  };
  keyCounters: KeyCounters;
  layerGroups: LayerGroups;
  tabNoteOverrides: TabNoteOverrides;
  tabCssOverrides: TabCssOverrides;
}
