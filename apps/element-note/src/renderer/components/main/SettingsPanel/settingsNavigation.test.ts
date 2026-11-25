import { describe, expect, it } from 'vitest';
import {
  SETTINGS_SEARCH_ITEMS,
  SETTINGS_SECTIONS,
  searchSettings,
} from './settingsNavigation';

const copy: Record<string, string> = {
  'settingsWorkspace.counts': 'Press counts',
  'settingsWorkspace.countsHelp':
    'Select a key, then choose Counts to show or hide its count.',
  'settingsWorkspace.trails': '레이닝 효과',
  'settingsWorkspace.trailsHelp': '키를 누른 시간만큼 레이닝이 길어집니다.',
  'settingsWorkspace.viewer': 'Viewer',
};
const translate = (key: string) => copy[key] ?? key;

describe('settings sections', () => {
  it('start with where the key viewer is shown and have no broadcast page', () => {
    expect(SETTINGS_SECTIONS[0]).toBe('outputs');
    expect(SETTINGS_SECTIONS).not.toContain('broadcast');
    // Presets are kept as files from the key viewer gallery, not from here.
    expect(SETTINGS_SECTIONS).not.toContain('presets');
    expect(SETTINGS_SECTIONS).toHaveLength(7);
  });

  it('list the three places and their rows under that one section', () => {
    expect(
      SETTINGS_SEARCH_ITEMS.filter((item) => item.section === 'outputs').map(
        (item) => item.label,
      ),
    ).toEqual([
      'outputs.game',
      'outputs.keyLimiter',
      'outputs.desktop',
      'outputs.lock',
      'outputs.onTop',
      'outputs.anchor',
      'outputs.obs',
      'outputs.address',
    ]);
    // Every result opens a section that exists, and no label repeats.
    const labels = SETTINGS_SEARCH_ITEMS.map((item) => item.label);
    expect(new Set(labels).size).toBe(labels.length);
    for (const item of SETTINGS_SEARCH_ITEMS)
      expect(SETTINGS_SECTIONS).toContain(item.section);
  });
});

describe('settings search', () => {
  it('finds the game, the desktop and OBS under where to show', () => {
    const words: Record<string, string> = {
      'outputs.game': '얼불춤 안',
      'outputs.desktop': '바탕화면 위',
      'outputs.obs': 'OBS 방송 화면 (부가 기능)',
      'settingsWorkspace.outputs': '보여 줄 곳',
    };
    const say = (key: string) => words[key] ?? key;
    for (const [query, label] of [
      ['얼불춤', 'outputs.game'],
      ['바탕화면', 'outputs.desktop'],
      ['obs 방송', 'outputs.obs'],
    ])
      expect(searchSettings(query, say)).toEqual([
        expect.objectContaining({ label, section: 'outputs' }),
      ]);
  });

  it('matches words across the setting name and its explanation', () => {
    expect(searchSettings('  PRESS   key ', translate)).toEqual([
      expect.objectContaining({
        label: 'settingsWorkspace.counts',
        section: 'viewer',
      }),
    ]);
  });

  it('finds Korean words without requiring a full title', () => {
    expect(searchSettings('레이닝', translate)).toEqual([
      expect.objectContaining({ label: 'settingsWorkspace.trails' }),
    ]);
  });

  it('does not show unrelated entries for an empty or unmatched query', () => {
    expect(searchSettings('   ', translate)).toEqual([]);
    expect(searchSettings('missing-setting', translate)).toEqual([]);
  });
});
