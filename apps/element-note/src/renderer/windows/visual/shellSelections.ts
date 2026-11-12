import { keyPositionSchema, type KeyPosition } from '@src/types/key/keys';
import type { GraphItemPosition } from '@src/types/key/graphItems';
import type { KnobItemPosition } from '@src/types/key/knobs';
import type { StatItemPosition } from '@src/types/key/statItems';
import { createDefaultGhostSettings } from '@src/renderer/editor/model/ghostKeys';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import type {
  PluginDefinitionInternal,
  PluginDisplayElementInternal,
} from '@src/types/plugin/api';
import {
  makeSpritePose,
  makeSpritePosition,
} from '@utils/sprite/spriteFixtures';

/** A picture that is not the same on every side, so a turn can be seen. */
const sampleImage = (fill: string) =>
  'data:image/svg+xml;utf8,' +
  encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="96" height="96">' +
      `<rect width="96" height="96" rx="20" fill="${fill}"/>` +
      '<circle cx="34" cy="36" r="14" fill="#1b1b1d"/>' +
      '<rect x="22" y="66" width="52" height="10" rx="5" fill="#1b1b1d"/></svg>',
  );
const SAMPLE_IMAGE = sampleImage('#f59e0b');
const SAMPLE_LOOK_IMAGE = sampleImage('#38bdf8');
const SAMPLE_IMAGE_SIZE = { width: 96, height: 96 };

/** Keys that looks can name: the first one is the key the fixture draws. */
const SAMPLE_KEYS = [
  { id: 'fixture-key-space', key: 'SPACE' },
  { id: 'fixture-key-a', key: 'A' },
  { id: 'fixture-key-s', key: 'S' },
  { id: 'fixture-key-d', key: 'D' },
];

const box = (overrides: Partial<KeyPosition>): KeyPosition =>
  keyPositionSchema.parse({
    dx: 120,
    dy: 90,
    width: 200,
    height: 100,
    count: 0,
    noteColor: '#f59e0b',
    noteOpacity: 100,
    useInlineStyles: true,
    ...overrides,
  });

/**
 * The selections of the shell fixture that are not a plain key:
 * `selection=sprite`, `stat`, `graph`, `knob` and `plugin` select one
 * element of that kind, `graphs` selects two graphs, and `rich` selects a key
 * that has a ghost, a sound and colors of its own. Any other value is left as
 * the fixture set it.
 *
 * The image of `selection=sprite` has two looks, one of them still waiting
 * for a key, on a tab with four keys. `sprite=once` makes it play once per
 * press, `sprite=custom` gives it a curve that is none of the named feels,
 * and `sprite=empty` is an image as it is first added: no picture, no looks.
 */
export function applyShellSelection(
  selection: string | null,
  viewer: string,
): void {
  const select = (type: 'sprite' | 'stat' | 'graph' | 'knob' | 'key') =>
    useGridSelectionStore.setState({
      selectedElements: [{ type, id: `${type}-0`, index: 0 }],
    });

  if (selection === 'sprite') {
    const variant = new URLSearchParams(window.location.search).get('sprite');
    useKeyStore.setState((state) => {
      const first = state.positions[viewer]?.[0];
      return {
        keyMappings: {
          ...state.keyMappings,
          [viewer]: SAMPLE_KEYS.map((item) => item.key),
        },
        positions: {
          ...state.positions,
          [viewer]: SAMPLE_KEYS.map((item, index) =>
            index === 0 && first
              ? { ...first, id: item.id }
              : box({ id: item.id, dx: 60 * index, dy: 300, width: 56 }),
          ),
        },
      };
    });
    useSpriteStore.setState({
      positions: {
        [viewer]: [
          variant === 'empty'
            ? makeSpritePosition({
                id: 'fixture-sprite',
                dx: 120,
                dy: 220,
                width: 140,
                height: 140,
                zIndex: 1,
                transitionMs: 0,
              })
            : makeSpritePosition({
                id: 'fixture-sprite',
                dx: 120,
                dy: 220,
                width: 140,
                height: 140,
                zIndex: 1,
                baseImage: SAMPLE_IMAGE,
                referenceNaturalSize: {
                  source: SAMPLE_IMAGE,
                  ...SAMPLE_IMAGE_SIZE,
                },
                activation: variant === 'once' ? 'onPress' : 'whileHeld',
                transitionMs: 300,
                ...(variant === 'custom'
                  ? { transitionEasing: 'steps(4, end)' }
                  : {}),
                poses: [
                  makeSpritePose({
                    poseId: 'tap',
                    triggers: ['fixture-key-space'],
                    transform: { x: 0, y: 18, rotation: -18, scale: 1 },
                    imageOverride: SAMPLE_LOOK_IMAGE,
                    imageOverrideMetrics: {
                      source: SAMPLE_LOOK_IMAGE,
                      ...SAMPLE_IMAGE_SIZE,
                    },
                  }),
                  makeSpritePose({
                    poseId: 'hold',
                    triggers: [],
                    transform: { x: 26, y: 0, rotation: 12, scale: 1.15 },
                  }),
                ],
              }),
        ],
      },
    });
    select('sprite');
  } else if (selection === 'stat') {
    useStatItemStore.setState({
      positions: {
        [viewer]: [
          { ...box({ width: 96, height: 60 }), statType: 'kps' },
        ] as StatItemPosition[],
      },
    });
    select('stat');
  } else if (selection === 'graph') {
    useGraphItemStore.setState({
      positions: {
        [viewer]: [
          { ...box({}), statType: 'kps', graphType: 'line' },
        ] as GraphItemPosition[],
      },
    });
    select('graph');
  } else if (selection === 'graphs') {
    useGraphItemStore.setState({
      positions: {
        [viewer]: [
          { ...box({}), statType: 'kps', graphType: 'line' },
          { ...box({ dy: 220 }), statType: 'total', graphType: 'bar' },
        ] as GraphItemPosition[],
      },
    });
    useGridSelectionStore.setState({
      selectedElements: [
        { type: 'graph', id: 'graph-0', index: 0 },
        { type: 'graph', id: 'graph-1', index: 1 },
      ],
    });
  } else if (selection === 'plugin') {
    const definition = {
      id: 'fixture:meter',
      pluginId: 'fixture',
      name: 'Input Meter',
      resizable: true,
      settings: {
        color: { type: 'color', default: '#f59e0b', label: 'Color' },
        speed: { type: 'number', default: 2, label: 'Speed', min: 0, max: 10 },
        peak: { type: 'boolean', default: true, label: 'Show peak' },
      },
    } as unknown as PluginDefinitionInternal;
    usePluginDisplayElementStore.setState({
      definitions: new Map([[definition.id, definition]]),
      elements: [
        {
          fullId: 'fixture:meter:1',
          pluginId: 'fixture',
          definitionId: definition.id,
          position: { x: 40, y: 60 },
          measuredSize: { width: 200, height: 120 },
          settings: {},
          tabId: viewer,
        },
      ] as unknown as PluginDisplayElementInternal[],
    });
    useGridSelectionStore.setState({
      selectedElements: [{ type: 'plugin', id: 'fixture:meter:1' }],
    });
  } else if (selection === 'knob') {
    useKnobItemStore.setState({
      positions: {
        [viewer]: [
          {
            ...box({ width: 80, height: 80 }),
            axisId: 'HIDA:Fixture Knob:X',
            sensitivity: 1,
          },
        ] as KnobItemPosition[],
      },
    });
    select('knob');
  } else if (selection === 'rich') {
    useKeyStore.setState((state) => ({
      positions: {
        ...state.positions,
        [viewer]: (state.positions[viewer] ?? []).map((key, index) =>
          index === 0
            ? {
                ...key,
                backgroundColor: '#1f2937',
                borderColor: '#f59e0b',
                fontColor: '#f9fafb',
                soundEnabled: true,
                soundPath: '/fixtures/mechanical.wav',
                ghost: {
                  enabled: true,
                  key: 'ENTER',
                  settings: {
                    ...createDefaultGhostSettings(key),
                    backgroundColor: '#312e81',
                    noteColor: '#818cf8',
                  },
                },
              }
            : key,
        ),
      },
    }));
    select('key');
  }
}
