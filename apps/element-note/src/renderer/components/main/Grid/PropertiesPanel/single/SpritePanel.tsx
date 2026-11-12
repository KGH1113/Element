import React, { useEffect, useRef, useState } from 'react';
import PropertySection from '../PropertySection';
import { useInspectorPages } from '../inspectorPageContext';
import { PropertyRow, TextInput } from '../PropertyInputs';
import { NumberField } from '../PartControls';
import Toggle from '@components/main/common/Toggle';
import ImagePicker from '@components/main/Modal/content/pickers/ImagePicker';
import { Dimmed } from '@components/main/Sidebar/SidebarParts';
import {
  applySpriteImage,
  type SpriteDraftError,
} from '@components/main/Modal/content/editors/spriteEditorDraft';
import {
  editSprite,
  saveSpritePreview,
} from '@src/renderer/editor/runtime/spritePositions';
import { useSpriteTryOutStore } from '@src/renderer/editor/runtime/spriteTryOut';
import { useKeyStore } from '@stores/data/useKeyStore';
import { probeImageSize } from '@utils/media/assetProbe';
import {
  isRenderableImageRef,
  resolveImageSource,
} from '@utils/media/imageSource';
import { resolvePoseImage } from '@utils/sprite/poseResolver';
import {
  SPRITE_ANCHOR_PRESETS,
  isSameSpriteAnchor,
} from '@utils/sprite/spriteGeometry';
import {
  spritePoseLook,
  spriteRestLook,
  type SpriteLook,
} from '@utils/sprite/spriteLooks';
import {
  spritePivotChangePatch,
  spritePosePivotChangePatch,
} from '@utils/sprite/spritePlacement';
import {
  SPRITE_CONSTRAINTS,
  type ReactiveSpritePosition,
  type SpriteAnchor,
  type SpritePose,
  type SpriteTransform,
} from '@src/types/key/sprites';
import PivotPad from './sprite/PivotPad';
import SpriteStage from './sprite/SpriteStage';
import TryItButton from './sprite/TryItButton';
import {
  SPRITE_PARTS,
  imageFileName,
  lookIdOfPart,
  spriteKeyOptions,
  spriteLookLabel,
  type SpriteKeyOption,
} from './spriteParts';
import './sprite/sprite.css';

type Translate = (key: string, options?: Record<string, unknown>) => string;
type SpriteChange = (sprite: ReactiveSpritePosition) => ReactiveSpritePosition;
/**
 * Changes the sprite; says why when the change cannot be saved. A step of a
 * drag is written with `save: false` and the drag is saved when it ends.
 */
type SpriteEdit = (
  change: SpriteChange,
  options?: { save?: boolean },
) => SpriteDraftError | null;

/** Text that is saved when the field is left. */
function TextField({
  value,
  label,
  placeholder,
  onCommit,
}: {
  value: string;
  label: string;
  placeholder?: string;
  onCommit: (value: string) => void;
}) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  return (
    <TextInput
      value={draft}
      ariaLabel={label}
      placeholder={placeholder}
      width="130px"
      onChange={setDraft}
      onBlur={() => {
        if (draft !== value) onCommit(draft);
      }}
    />
  );
}

/** How far a look is moved, how much it is turned and how big it is. */
function LookRows({
  value,
  onChange,
  t,
}: {
  value: SpriteTransform;
  onChange: (value: SpriteTransform) => void;
  t: Translate;
}) {
  const { offset, rotation, scale } = SPRITE_CONSTRAINTS;
  return (
    <>
      <PropertyRow label={t('sprite.move')}>
        <NumberField
          prefix="X"
          ariaLabel={`${t('sprite.move')} X`}
          value={value.x}
          min={offset.min}
          max={offset.max}
          onChange={(x) => onChange({ ...value, x })}
        />
        <NumberField
          prefix="Y"
          ariaLabel={`${t('sprite.move')} Y`}
          value={value.y}
          min={offset.min}
          max={offset.max}
          onChange={(y) => onChange({ ...value, y })}
        />
      </PropertyRow>
      <PropertyRow label={t('sprite.rotation')}>
        <NumberField
          suffix="°"
          ariaLabel={t('sprite.rotation')}
          value={value.rotation}
          digits={1}
          min={rotation.min}
          max={rotation.max}
          onChange={(next) => onChange({ ...value, rotation: next })}
        />
      </PropertyRow>
      <PropertyRow label={t('sprite.size')}>
        <NumberField
          suffix={t('sprite.times')}
          ariaLabel={t('sprite.size')}
          value={value.scale}
          digits={2}
          step={0.1}
          min={scale.min}
          max={scale.max}
          onChange={(next) => onChange({ ...value, scale: next })}
        />
      </PropertyRow>
    </>
  );
}

/** The nine usual places of a turning centre, in the order they are marked. */
const CENTER_NAME_KEYS = [
  'sprite.centerTopLeft',
  'sprite.centerTop',
  'sprite.centerTopRight',
  'sprite.centerLeft',
  'sprite.centerMiddle',
  'sprite.centerRight',
  'sprite.centerBottomLeft',
  'sprite.centerBottom',
  'sprite.centerBottomRight',
] as const;

const centerName = (anchor: SpriteAnchor, t: Translate): string => {
  const at = SPRITE_ANCHOR_PRESETS.findIndex((preset) =>
    isSameSpriteAnchor(preset, anchor),
  );
  return t(at >= 0 ? CENTER_NAME_KEYS[at] : 'sprite.centerCustom');
};

/**
 * The point a look turns and grows around, placed on its picture. The
 * picture stays where it is while the point moves; `place` says how.
 */
function CenterPad({
  sprite,
  look,
  value,
  place,
  t,
}: {
  sprite: ReactiveSpritePosition;
  look: SpriteLook;
  value: SpriteAnchor;
  /**
   * Moves the point of the sprite as it was when the point was picked up.
   * Nothing is saved until it is put down.
   */
  place: (from: ReactiveSpritePosition, anchor: SpriteAnchor) => void;
  t: Translate;
}) {
  const from = useRef(sprite);
  const { rect } = look.placement;
  return (
    <PivotPad
      src={resolveImageSource(look.src)}
      aspect={rect.width / rect.height}
      value={value}
      label={t('sprite.center')}
      nameOf={(anchor) => centerName(anchor, t)}
      onStart={() => {
        from.current = sprite;
      }}
      onPlace={(anchor) => place(from.current, anchor)}
      onEnd={() => void saveSpritePreview()}
    />
  );
}

/**
 * The keys a look is shown for. Keys picked here that another look already
 * has cannot be saved; they stay picked on this page until they differ.
 */
function LookKeys({
  pose,
  keys,
  edit,
  t,
}: {
  pose: SpritePose;
  keys: readonly SpriteKeyOption[];
  edit: SpriteEdit;
  t: Translate;
}) {
  const [unsaved, setUnsaved] = useState<{
    triggers: string[];
    reason: SpriteDraftError;
  } | null>(null);
  const picked = unsaved?.triggers ?? pose.triggers;

  const toggle = (id: string) => {
    const known = new Set(keys.map((key) => key.id));
    // Keys that were deleted from the tab are dropped on the way.
    const next = (
      picked.includes(id)
        ? picked.filter((trigger) => trigger !== id)
        : [...picked, id]
    ).filter((trigger) => known.has(trigger));
    const reason = edit((sprite) => ({
      ...sprite,
      poses: sprite.poses.map((item) =>
        item.poseId === pose.poseId ? { ...item, triggers: next } : item,
      ),
    }));
    setUnsaved(reason ? { triggers: next, reason } : null);
  };

  // One line under the keys: what is wrong, or else how keys are read.
  const problem =
    keys.length === 0
      ? t('sprite.noKeysHint')
      : unsaved?.reason === 'duplicateTriggers'
        ? t('sprite.sameKeysHint')
        : unsaved
          ? t('sprite.notSaved')
          : picked.length === 0
            ? t('sprite.needsKeyHint')
            : '';

  return (
    <PropertySection id="sprite-look-keys" title={t('sprite.lookKeys')}>
      <div className="en-chips" role="group" aria-label={t('sprite.lookKeys')}>
        {keys.map((key) => (
          <button
            key={key.id}
            type="button"
            className="en-chip"
            aria-pressed={picked.includes(key.id)}
            onClick={() => toggle(key.id)}
          >
            {key.label}
          </button>
        ))}
      </div>
      <p
        className="en-hint"
        role="status"
        data-tone={problem ? 'attention' : undefined}
      >
        {problem || t('sprite.lookKeysHint')}
      </p>
    </PropertySection>
  );
}

/**
 * Makes the picked file the image of a sprite, or the picture of one of its
 * looks. False when the file cannot be used.
 */
const setSpriteImage = async (
  mode: string,
  spriteId: string,
  source: string,
  forPoseId: string | null,
): Promise<boolean> => {
  const size = await probeImageSize(source).catch(() => null);
  const { dimensionMax } = SPRITE_CONSTRAINTS.imageMetrics;
  if (!size || size.width > dimensionMax || size.height > dimensionMax) {
    return false;
  }
  return (
    editSprite(mode, spriteId, (current) =>
      applySpriteImage(current, source, size, forPoseId),
    ) === null
  );
};

interface SpriteImageChooserProps {
  /** The tab the sprite is on. */
  mode: string;
  sprite: ReactiveSpritePosition;
  /** The look whose own picture is chosen; the image itself when left out. */
  poseId?: string | null;
  /** The control that opened the chooser. */
  trigger: React.RefObject<HTMLElement | null>;
  onClose: () => void;
  t: Translate;
}

/**
 * Chooses the image of a sprite, or the picture of one of its looks. It
 * opens beside the control that names the image: there is no page
 * that only leads to it. A file that cannot be used is said so right there.
 */
export function SpriteImageChooser({
  mode,
  sprite,
  poseId = null,
  trigger,
  onClose,
  t,
}: SpriteImageChooserProps) {
  const [failed, setFailed] = useState(false);
  const pose = poseId
    ? sprite.poses.find((item) => item.poseId === poseId)
    : null;
  return (
    <ImagePicker
      open
      referenceRef={trigger as React.RefObject<HTMLElement>}
      idleImage={(pose ? pose.imageOverride : sprite.baseImage) || ''}
      chooseLabel={t('sprite.chooseImage')}
      notice={failed ? t('sprite.imageError') : ''}
      onIdleImageChange={(source) =>
        void setSpriteImage(mode, sprite.id!, source, poseId).then((taken) =>
          setFailed(!taken),
        )
      }
      onIdleImageReset={() => {
        setFailed(false);
        editSprite(mode, sprite.id, (current) =>
          poseId
            ? {
                ...current,
                poses: current.poses.map((item) =>
                  item.poseId === poseId
                    ? {
                        ...item,
                        imageOverride: null,
                        imageOverrideMetrics: null,
                      }
                    : item,
                ),
              }
            : {
                ...current,
                baseImage: null,
                // Looks keep their size: only the link to the file is dropped.
                referenceNaturalSize: current.referenceNaturalSize
                  ? { ...current.referenceNaturalSize, source: null }
                  : null,
              },
        );
      }}
      onClose={onClose}
    />
  );
}

/** The picture of a look: a button that names it and opens the chooser. */
function LookImageRow({
  mode,
  sprite,
  pose,
  t,
}: {
  mode: string;
  sprite: ReactiveSpritePosition;
  pose: SpritePose;
  t: Translate;
}) {
  const trigger = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState(false);
  const label = t('sidebar.addSprite');
  const own = isRenderableImageRef(pose.imageOverride);
  const name = own
    ? // A picture that has no file name is still a picture of its own.
      imageFileName(pose.imageOverride) || t('sprite.ownImage')
    : t('sprite.sameImage');
  const picture = resolveImageSource(
    resolvePoseImage(pose.imageOverride, sprite.baseImage),
  );
  return (
    <PropertyRow label={label}>
      <button
        ref={trigger}
        type="button"
        className="en-pick"
        aria-label={label}
        aria-haspopup="dialog"
        aria-expanded={open}
        title={name}
        onClick={() => setOpen((shown) => !shown)}
      >
        {picture && <img src={picture} alt="" draggable={false} />}
        <span>{name}</span>
      </button>
      {open && (
        <SpriteImageChooser
          mode={mode}
          sprite={sprite}
          poseId={pose.poseId}
          trigger={trigger}
          onClose={() => setOpen(false)}
          t={t}
        />
      )}
    </PropertyRow>
  );
}

interface SpritePanelProps {
  /** The tab the sprite is on. */
  mode: string;
  sprite: ReactiveSpritePosition;
  useCustomCSS: boolean;
  /** Returns to the image's own page: the look that was open is gone. */
  onLeavePage: () => void;
  t: Translate;
}

/**
 * The pages of an image: its resting look, one for each look that keys
 * bring up, and its CSS. The sidebar says which page is open; every change
 * is shown at once and saved (see spritePositions).
 */
const SpritePanel: React.FC<SpritePanelProps> = ({
  mode,
  sprite,
  useCustomCSS,
  onLeavePage,
  t,
}) => {
  const page = useInspectorPages()?.activePage ?? null;
  const keyPositions = useKeyStore((state) => state.positions[mode]);
  const keyMappings = useKeyStore((state) => state.keyMappings[mode]);
  const trying = useSpriteTryOutStore((state) => state.held);

  const edit: SpriteEdit = (change, options) =>
    editSprite(mode, sprite.id, change, options);
  const editPose = (
    poseId: string,
    change: (pose: SpritePose) => SpritePose,
    options?: { save?: boolean },
  ) =>
    edit(
      (current) => ({
        ...current,
        poses: current.poses.map((pose) =>
          pose.poseId === poseId ? change(pose) : pose,
        ),
      }),
      options,
    );
  /** A shift that changes nothing is not written. */
  const shifted = (transform: SpriteTransform, to: { x: number; y: number }) =>
    transform.x === to.x && transform.y === to.y
      ? transform
      : { ...transform, ...to };

  const rest = spriteRestLook(sprite);

  if (page === SPRITE_PARTS.rest) {
    return (
      <>
        <section className="en-section">
          <div className="en-section-card">
            <SpriteStage
              sprite={sprite}
              look={rest}
              label={t('sprite.rest')}
              hint={t('sprite.dragToMove')}
              onMove={(to) =>
                edit(
                  (current) => {
                    const idleTransform = shifted(current.idleTransform, to);
                    return idleTransform === current.idleTransform
                      ? current
                      : { ...current, idleTransform };
                  },
                  { save: false },
                )
              }
              onMoveEnd={() => void saveSpritePreview()}
            />
          </div>
        </section>
        <PropertySection id="sprite-look" title={t('sprite.appearance')}>
          <LookRows
            value={sprite.idleTransform}
            onChange={(idleTransform) =>
              edit((current) => ({ ...current, idleTransform }))
            }
            t={t}
          />
        </PropertySection>
        <PropertySection
          id="sprite-center"
          title={t('sprite.center')}
          description={t('sprite.centerHint')}
        >
          <CenterPad
            sprite={sprite}
            look={rest}
            value={sprite.pivot}
            place={(from, pivot) =>
              edit(
                (current) => {
                  const patch = spritePivotChangePatch(from, pivot);
                  return patch ? { ...current, ...patch } : current;
                },
                { save: false },
              )
            }
            t={t}
          />
        </PropertySection>
      </>
    );
  }

  if (page === SPRITE_PARTS.css && useCustomCSS) {
    return (
      <PropertySection id="sprite-css" title={t('propertiesPanel.sectionCss')}>
        <PropertyRow label={t('propertiesPanel.className')}>
          <TextField
            value={sprite.className || ''}
            label={t('propertiesPanel.className')}
            placeholder="className"
            onCommit={(className) =>
              edit((current) => ({
                ...current,
                className: className.trim() || null,
              }))
            }
          />
        </PropertyRow>
        <PropertyRow label={t('propertiesPanel.useInlineStyles')}>
          <Toggle
            checked={sprite.useInlineStyles === true}
            ariaLabel={t('propertiesPanel.useInlineStyles')}
            onChange={(useInlineStyles) =>
              edit((current) => ({ ...current, useInlineStyles }))
            }
          />
        </PropertyRow>
      </PropertySection>
    );
  }

  const poseId = lookIdOfPart(page);
  const poseIndex = sprite.poses.findIndex((item) => item.poseId === poseId);
  const pose = poseIndex >= 0 ? sprite.poses[poseIndex] : null;
  if (!pose) return null;

  const keys = spriteKeyOptions(keyPositions ?? [], keyMappings ?? []);
  const look = spritePoseLook(sprite, pose);
  const followsRest = pose.pivot == null;
  /** Moves this look's own centre; its picture stays where it is. */
  const placeCenter = (
    from: ReactiveSpritePosition,
    pivot: SpriteAnchor | null,
    options?: { save?: boolean },
  ) => {
    const before = from.poses.find((item) => item.poseId === pose.poseId);
    const patch = before && spritePosePivotChangePatch(from, before, pivot);
    if (patch) {
      editPose(pose.poseId, (item) => ({ ...item, ...patch }), options);
    }
  };

  return (
    <>
      <section className="en-section">
        <div className="en-section-card">
          <SpriteStage
            sprite={sprite}
            look={look}
            rest={rest}
            label={spriteLookLabel(pose, poseIndex, t)}
            hint={t('sprite.dragToMove')}
            onMove={(to) =>
              editPose(
                pose.poseId,
                (item) => {
                  const transform = shifted(item.transform, to);
                  return transform === item.transform
                    ? item
                    : { ...item, transform };
                },
                { save: false },
              )
            }
            onMoveEnd={() => void saveSpritePreview()}
            motion={{
              playing: trying,
              rest: sprite.idleTransform,
              activation: sprite.activation,
              changeMs: sprite.transitionMs,
              returnMs: sprite.pressDurationMs,
              easing: sprite.transitionEasing,
            }}
          />
          <TryItButton
            spriteId={sprite.id!}
            poseId={pose.poseId}
            label={t('sprite.tryIt')}
          />
        </div>
      </section>
      <LookKeys key={pose.poseId} pose={pose} keys={keys} edit={edit} t={t} />
      <PropertySection id="sprite-look" title={t('sprite.appearance')}>
        <PropertyRow label={t('sprite.lookName')}>
          <TextField
            key={pose.poseId}
            value={pose.name || ''}
            label={t('sprite.lookName')}
            placeholder={spriteLookLabel({ name: null }, poseIndex, t)}
            onCommit={(name) =>
              editPose(pose.poseId, (item) => ({
                ...item,
                name: name.trim() || null,
              }))
            }
          />
        </PropertyRow>
        <LookImageRow mode={mode} sprite={sprite} pose={pose} t={t} />
        <LookRows
          value={pose.transform}
          onChange={(transform) =>
            editPose(pose.poseId, (item) => ({ ...item, transform }))
          }
          t={t}
        />
      </PropertySection>
      <PropertySection
        id="sprite-center"
        title={t('sprite.center')}
        description={t('sprite.centerHint')}
      >
        <PropertyRow label={t('sprite.sameCenter')}>
          <Toggle
            checked={followsRest}
            ariaLabel={t('sprite.sameCenter')}
            onChange={(follow) =>
              placeCenter(sprite, follow ? null : { ...sprite.pivot })
            }
          />
        </PropertyRow>
        <Dimmed off={followsRest}>
          <CenterPad
            sprite={sprite}
            look={look}
            value={pose.pivot ?? sprite.pivot}
            place={(from, pivot) => placeCenter(from, pivot, { save: false })}
            t={t}
          />
        </Dimmed>
      </PropertySection>
      <section className="en-section">
        <div className="en-section-card">
          <button
            type="button"
            className="en-text-button"
            data-tone="danger"
            onClick={() => {
              onLeavePage();
              edit((current) => ({
                ...current,
                poses: current.poses.filter(
                  (item) => item.poseId !== pose.poseId,
                ),
              }));
            }}
          >
            {t('sprite.removeLook')}
          </button>
        </div>
      </section>
    </>
  );
};

export default SpritePanel;
