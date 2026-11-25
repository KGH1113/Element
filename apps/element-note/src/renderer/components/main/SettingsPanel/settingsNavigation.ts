export const SETTINGS_SECTIONS = [
  // Where the key viewer is shown comes first: it is what the app is for.
  'outputs',
  'viewer',
  'editor',
  'sound',
  'app',
  'extensions',
  'advanced',
] as const;

export type SettingsSection = (typeof SETTINGS_SECTIONS)[number];

export const SETTINGS_SEARCH_ITEMS: {
  section: SettingsSection;
  label: string;
  hint: string;
}[] = [
  {
    section: 'outputs',
    label: 'outputs.game',
    hint: 'outputs.gameHelp',
  },
  {
    section: 'outputs',
    label: 'outputs.keyLimiter',
    hint: 'outputs.keyLimiterHelp',
  },
  {
    section: 'outputs',
    label: 'outputs.desktop',
    hint: 'outputs.desktopHelp',
  },
  {
    section: 'outputs',
    label: 'outputs.lock',
    hint: 'settingsWorkspace.lockHelp',
  },
  {
    section: 'outputs',
    label: 'outputs.onTop',
    hint: 'settingsWorkspace.topHelp',
  },
  {
    section: 'outputs',
    label: 'outputs.anchor',
    hint: 'settingsWorkspace.anchorHelp',
  },
  {
    section: 'outputs',
    label: 'outputs.obs',
    hint: 'outputs.obsHelp',
  },
  {
    section: 'outputs',
    label: 'outputs.address',
    hint: 'outputs.addressHelp',
  },
  {
    section: 'viewer',
    label: 'settingsWorkspace.trails',
    hint: 'settingsWorkspace.trailsHelp',
  },
  {
    section: 'viewer',
    label: 'settingsWorkspace.counts',
    // Counts are controlled per key; this result opens the editor guidance.
    hint: 'settingsWorkspace.countsHelp',
  },
  {
    section: 'viewer',
    label: 'settingsWorkspace.background',
    hint: 'settingsWorkspace.backgroundHelp',
  },
  {
    section: 'viewer',
    label: 'raining.speed',
    hint: 'raining.speedHelp',
  },
  {
    section: 'viewer',
    label: 'raining.length',
    hint: 'raining.lengthHelp',
  },
  {
    section: 'viewer',
    label: 'raining.downward',
    hint: 'raining.downwardHelp',
  },
  {
    section: 'viewer',
    label: 'raining.fade',
    hint: 'raining.fadeHelp',
  },
  {
    section: 'viewer',
    label: 'raining.shortSame',
    hint: 'raining.shortSameHelp',
  },
  {
    section: 'viewer',
    label: 'raining.keyDelay',
    hint: 'raining.keyDelayHelp',
  },
  {
    section: 'advanced',
    label: 'raining.frameLimit',
    hint: 'raining.frameLimitHelp',
  },
  {
    section: 'viewer',
    label: 'gridSettings.overlayPadding',
    hint: 'settingsWorkspace.paddingHelp',
  },
  {
    section: 'editor',
    label: 'gridSettings.gridSnapSize',
    hint: 'settingsWorkspace.snapHelp',
  },
  {
    section: 'editor',
    label: 'gridSettings.minimapEnabled',
    hint: 'settingsWorkspace.minimapHelp',
  },
  {
    section: 'editor',
    label: 'gridSettings.alignmentGuides',
    hint: 'settingsWorkspace.alignHelp',
  },
  {
    section: 'editor',
    label: 'gridSettings.spacingGuides',
    hint: 'settingsWorkspace.spacingHelp',
  },
  {
    section: 'editor',
    label: 'gridSettings.sizeMatchGuides',
    hint: 'settingsWorkspace.sizeHelp',
  },
  {
    section: 'sound',
    label: 'settings.keySoundOutput',
    hint: 'settingsWorkspace.soundHelp',
  },
  {
    section: 'sound',
    label: 'settings.keySoundOutputBuffer',
    hint: 'settingsWorkspace.bufferHelp',
  },
  {
    section: 'app',
    label: 'settings.language',
    hint: 'settingsWorkspace.appHelp',
  },
  {
    section: 'app',
    label: 'settingsWorkspace.keepRunning',
    hint: 'settingsWorkspace.keepRunningHelp',
  },
  {
    section: 'app',
    label: 'settings.shortcuts',
    hint: 'settingsWorkspace.shortcutsHelp',
  },
  {
    section: 'extensions',
    label: 'settingsWorkspace.css',
    hint: 'settingsWorkspace.cssHelp',
  },
  {
    section: 'extensions',
    label: 'settingsWorkspace.plugins',
    hint: 'settingsWorkspace.pluginsHelp',
  },
  {
    section: 'advanced',
    label: 'settings.graphicsOption',
    hint: 'settingsWorkspace.graphicsHelp',
  },
  {
    section: 'advanced',
    label: 'settings.developerMode',
    hint: 'settingsWorkspace.developerHelp',
  },
  {
    section: 'advanced',
    label: 'settings.resetData',
    hint: 'settingsWorkspace.resetHelp',
  },
];

export function searchSettings(
  query: string,
  translate: (key: string) => string,
) {
  const words = query.trim().toLocaleLowerCase().split(/\s+/).filter(Boolean);
  if (!words.length) return [];
  return SETTINGS_SEARCH_ITEMS.filter((item) => {
    const text = [item.label, item.hint, `settingsWorkspace.${item.section}`]
      .map(translate)
      .join(' ')
      .toLocaleLowerCase();
    return words.every((word) => text.includes(word));
  });
}
