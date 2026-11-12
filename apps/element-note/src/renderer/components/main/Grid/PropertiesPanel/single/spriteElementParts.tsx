import StepSlider from '@components/main/common/StepSlider';
import { createSpritePose } from '@components/main/Modal/content/editors/spriteEditorDraft';
import { RowImage, RowText } from '@components/main/Sidebar/SidebarParts';
import { PART_ICONS } from '@components/main/Sidebar/sidebarIcons';
import type {
  ElementPart,
  PagePart,
} from '@components/main/Sidebar/elementParts';
import { editSprite } from '@src/renderer/editor/runtime/spritePositions';
import {
  SPRITE_CONSTRAINTS,
  type ReactiveSpritePosition,
} from '@src/types/key/sprites';
import { resolveImageSource } from '@utils/media/imageSource';
import {
  frameSpriteLooks,
  spritePoseLook,
  spriteRestLook,
} from '@utils/sprite/spriteLooks';
import {
  SPRITE_CHANGE_STOPS_MS,
  SPRITE_REACTIONS,
  SPRITE_RETURN_STOPS_MS,
  feelOfEasing,
  spriteTimeText,
} from '@utils/sprite/spriteMotion';
import { poseNeedsKey } from '@utils/sprite/unsavedPoses';
import { SpriteImageChooser } from './SpritePanel';
import {
  SpriteFeelChooser,
  SpriteReactionChooser,
} from './sprite/SpriteMotionChoosers';
import SpriteLookThumb from './sprite/SpriteLookThumb';
import {
  SPRITE_PARTS,
  imageFileName,
  lookWhenText,
  spriteLookLabel,
  spriteLookPart,
  type SpriteKeyOption,
} from './spriteParts';

type Translate = (key: string, options?: Record<string, unknown>) => string;

interface SpritePartsInput {
  /** The tab the sprite is on. */
  mode: string;
  sprite: ReactiveSpritePosition;
  /** The keys of that tab, as looks can name them. */
  keys: readonly SpriteKeyOption[];
  /** The language times are written in. */
  language: string;
  useCustomCSS: boolean;
  t: Translate;
  /** Opens a page of the image: a look that was just added. */
  onOpenPart: (part: string) => void;
}

/**
 * The rows of an image's page, as data, in the groups they are shown in
 * (the image's box, which every element has, is added by the caller):
 *
 * - `image`: the picture itself, chosen in a chooser.
 * - `looks`: the resting look, then one row for each look that keys bring
 *   up, then the row that adds one. Each opens its page.
 * - `motion`: what a press does and how the change feels, both chosen from
 *   tiles beside the row, and the two times, set in their rows.
 * - `more`: the CSS page, while custom CSS is in use.
 */
export function spriteElementParts({
  mode,
  sprite,
  keys,
  language,
  useCustomCSS,
  t,
  onOpenPart,
}: SpritePartsInput): Record<
  'image' | 'looks' | 'motion' | 'more',
  ElementPart[]
> {
  const edit = (
    change: (current: ReactiveSpritePosition) => ReactiveSpritePosition,
  ) => editSprite(mode, sprite.id, change);

  const image = resolveImageSource(sprite.baseImage);
  const rest = spriteRestLook(sprite);
  const looks = sprite.poses.map((pose) => spritePoseLook(sprite, pose));
  // One frame for every row, so the rows differ exactly as the looks do.
  const frame = frameSpriteLooks(sprite, [rest, ...looks]);

  const once = sprite.activation === 'onPress';
  const reaction = SPRITE_REACTIONS.find(
    (item) => item.id === sprite.activation,
  );
  const feel = feelOfEasing(sprite.transitionEasing);
  const changeLabel = t('sprite.changeTime');
  const returnLabel = t('sprite.returnTime');
  const timeText = (ms: number) => spriteTimeText(ms, t, language);

  const lookPages: PagePart[] = sprite.poses.map((pose, index) => ({
    kind: 'page',
    id: spriteLookPart(pose.poseId),
    label: spriteLookLabel(pose, index, t),
    icon: <SpriteLookThumb sprite={sprite} look={looks[index]} frame={frame} />,
    groups: null,
    value: (
      <span
        className="en-row-value-text"
        data-tone={poseNeedsKey(pose) ? 'attention' : undefined}
      >
        {lookWhenText(pose, keys, t)}
      </span>
    ),
  }));

  return {
    image: [
      {
        kind: 'chooser',
        id: SPRITE_PARTS.image,
        label: t('sidebar.addSprite'),
        icon: PART_ICONS.image,
        value: (
          <>
            <RowText>
              {imageFileName(sprite.baseImage) ||
                (image ? '' : t('sprite.noImage'))}
            </RowText>
            {image && <RowImage src={image} />}
          </>
        ),
        chooser: ({ trigger, close }) => (
          <SpriteImageChooser
            mode={mode}
            sprite={sprite}
            trigger={trigger}
            onClose={close}
            t={t}
          />
        ),
      },
    ],
    looks: [
      {
        kind: 'page',
        id: SPRITE_PARTS.rest,
        label: t('sprite.rest'),
        icon: <SpriteLookThumb sprite={sprite} look={rest} frame={frame} />,
        groups: null,
        value: <RowText>{t('sprite.restWhen')}</RowText>,
      },
      ...lookPages,
      {
        kind: 'action',
        id: SPRITE_PARTS.addLook,
        label: t('sprite.addLook'),
        icon: PART_ICONS.add,
        value: <RowText>{t('sprite.addLookWhen')}</RowText>,
        disabled: sprite.poses.length >= SPRITE_CONSTRAINTS.maxPoses,
        run: () => {
          // It has no key yet, so it opens at once to be given one.
          const added = createSpritePose();
          const reason = edit((current) => ({
            ...current,
            poses: [...current.poses, added],
          }));
          if (!reason) onOpenPart(spriteLookPart(added.poseId));
        },
      },
    ],
    motion: [
      {
        kind: 'chooser',
        id: SPRITE_PARTS.reaction,
        label: t('sprite.reaction'),
        icon: PART_ICONS.input,
        value: <RowText>{reaction ? t(reaction.labelKey) : ''}</RowText>,
        chooser: ({ trigger, close }) => (
          <SpriteReactionChooser
            mode={mode}
            sprite={sprite}
            trigger={trigger}
            onClose={close}
            t={t}
          />
        ),
      },
      {
        kind: 'chooser',
        id: SPRITE_PARTS.feel,
        label: t('sprite.feel'),
        icon: PART_ICONS.feel,
        value: (
          <RowText>{t(feel ? feel.labelKey : 'sprite.feelCustom')}</RowText>
        ),
        chooser: ({ trigger, close }) => (
          <SpriteFeelChooser
            mode={mode}
            sprite={sprite}
            trigger={trigger}
            onClose={close}
            t={t}
          />
        ),
      },
      // Each time belongs to one way of reacting; the other one stays in
      // place, greyed out.
      {
        kind: 'inline',
        id: SPRITE_PARTS.changeTime,
        label: changeLabel,
        icon: PART_ICONS.time,
        disabled: once,
        control: (
          <StepSlider
            stops={SPRITE_CHANGE_STOPS_MS}
            value={sprite.transitionMs}
            ariaLabel={changeLabel}
            format={timeText}
            onCommit={(transitionMs) =>
              edit((current) => ({ ...current, transitionMs }))
            }
          />
        ),
      },
      {
        kind: 'inline',
        id: SPRITE_PARTS.returnTime,
        label: returnLabel,
        icon: PART_ICONS.time,
        disabled: !once,
        control: (
          <StepSlider
            stops={SPRITE_RETURN_STOPS_MS}
            value={sprite.pressDurationMs}
            ariaLabel={returnLabel}
            format={timeText}
            onCommit={(pressDurationMs) =>
              edit((current) => ({ ...current, pressDurationMs }))
            }
          />
        ),
      },
    ],
    more: useCustomCSS
      ? [
          {
            kind: 'page',
            id: SPRITE_PARTS.css,
            label: t('propertiesPanel.sectionCss'),
            icon: PART_ICONS.code,
            groups: null,
            value: <RowText>{sprite.className || ''}</RowText>,
          },
        ]
      : [],
  };
}
