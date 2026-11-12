import type { RefObject } from 'react';
import ChoiceTiles, { type Choice } from '../../ChoiceTiles';
import { editSprite } from '@src/renderer/editor/runtime/spritePositions';
import {
  CUSTOM_SPRITE_FEEL,
  SPRITE_FEELS,
  SPRITE_REACTIONS,
  easingOfFeel,
  feelOfEasing,
  type SpriteFeelId,
} from '@utils/sprite/spriteMotion';
import { resolveSpriteRenderEasing } from '@utils/sprite/spriteReach';
import type {
  ReactiveSpritePosition,
  SpriteActivation,
} from '@src/types/key/sprites';
import { FeelPicture, ReactionPicture } from './motionPictures';

interface ChooserProps {
  /** The tab the sprite is on. */
  mode: string;
  sprite: ReactiveSpritePosition;
  /** The row that opened the chooser. */
  trigger: RefObject<HTMLElement | null>;
  onClose: () => void;
  t: (key: string) => string;
}

/** Chooses what the image does when a key is pressed. */
export function SpriteReactionChooser({
  mode,
  sprite,
  trigger,
  onClose,
  t,
}: ChooserProps) {
  const choices: Choice<SpriteActivation>[] = SPRITE_REACTIONS.map(
    (reaction) => ({
      id: reaction.id,
      label: t(reaction.labelKey),
      picture: <ReactionPicture mode={reaction.id} />,
    }),
  );
  return (
    <ChoiceTiles
      title={t('sprite.reaction')}
      trigger={trigger}
      choices={choices}
      value={sprite.activation}
      onChoose={(activation) =>
        editSprite(mode, sprite.id, (current) => ({ ...current, activation }))
      }
      onClose={onClose}
    />
  );
}

type FeelChoice = SpriteFeelId | typeof CUSTOM_SPRITE_FEEL;

/**
 * Chooses how the image moves between looks. A stored curve that is none of
 * the feels is shown as one more choice, already chosen, and is kept until
 * another one is picked; a curve is never typed.
 */
export function SpriteFeelChooser({
  mode,
  sprite,
  trigger,
  onClose,
  t,
}: ChooserProps) {
  const feel = feelOfEasing(sprite.transitionEasing);
  const choices: Choice<FeelChoice>[] = SPRITE_FEELS.map((item) => ({
    id: item.id,
    label: t(item.labelKey),
    picture: <FeelPicture easing={item.easing} />,
  }));
  if (!feel) {
    choices.push({
      id: CUSTOM_SPRITE_FEEL,
      label: t('sprite.feelCustom'),
      picture: (
        <FeelPicture
          easing={resolveSpriteRenderEasing(sprite.transitionEasing)}
        />
      ),
      readOnly: true,
    });
  }
  return (
    <ChoiceTiles
      title={t('sprite.feel')}
      trigger={trigger}
      choices={choices}
      value={feel ? feel.id : CUSTOM_SPRITE_FEEL}
      columns={3}
      onChoose={(id) => {
        if (id === CUSTOM_SPRITE_FEEL) return;
        editSprite(mode, sprite.id, (current) => ({
          ...current,
          transitionEasing: easingOfFeel(id),
        }));
      }}
      onClose={onClose}
    />
  );
}
