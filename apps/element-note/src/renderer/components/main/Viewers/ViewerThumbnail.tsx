import { memo, type CSSProperties } from 'react';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import { computeKeyElementStyles } from '@hooks/overlay/useKeyElementStyles';
import { getKeyInfoByGlobalKey } from '@utils/core/KeyMaps';
import StatItem from '@components/overlay/counters/StatItem';
import OverlayKnobItem from '@components/overlay/counters/OverlayKnobItem';
import GraphPanel from '@components/shared/GraphPanel';
import SpriteRuntimeLayer from '@components/shared/SpriteRuntimeLayer';
import type { KeyPosition } from '@src/types/key/keys';
import { THUMBNAIL_SIZE } from './thumbnailScale';

const MARGIN = 10;

// A graph at rest has no history; this is the shape the editor shows too.
const GRAPH_HISTORY = [
  8, 10, 11, 13, 12, 14, 15, 16, 14, 13, 12, 14, 15, 14, 14,
];
const NO_KEYS: string[] = [];

interface Boxed {
  dx?: number;
  dy?: number;
  width?: number;
  height?: number;
  hidden?: boolean;
  zIndex?: number | null;
}

const visible = <T extends Boxed>(list: readonly T[] | undefined): T[] =>
  (list ?? []).filter((item) => item && !item.hidden);

/**
 * A key viewer as it really looks, small and still. It is drawn with the same
 * parts the overlay uses, at rest: nothing in it follows key presses, and
 * nothing in it can be pressed. Any key viewer can be shown, not only the one
 * on the canvas.
 */
function ViewerThumbnail({ viewerId }: { viewerId: string }) {
  const keys = useKeyStore((state) => state.keyMappings[viewerId]);
  const keyPositions = useKeyStore((state) => state.positions[viewerId]);
  const stats = visible(
    useStatItemStore((state) => state.positions[viewerId]) as
      (Boxed & Record<string, unknown>)[] | undefined,
  );
  const graphs = visible(
    useGraphItemStore((state) => state.positions[viewerId]) as
      (Boxed & Record<string, unknown>)[] | undefined,
  );
  const knobs = visible(
    useKnobItemStore((state) => state.positions[viewerId]) as
      (Boxed & Record<string, unknown>)[] | undefined,
  );
  const sprites = useSpriteStore((state) => state.positions[viewerId]) ?? [];

  const shownKeys = (keyPositions ?? [])
    .map((position, index) => ({ position, index }))
    .filter(({ position }) => position && !position.hidden);

  const boxes = [
    ...shownKeys.map(({ position }) => ({ box: position, w: 60, h: 60 })),
    ...stats.map((box) => ({ box, w: 60, h: 60 })),
    ...graphs.map((box) => ({ box, w: 200, h: 100 })),
    ...knobs.map((box) => ({ box, w: 60, h: 60 })),
    ...visible(sprites as Boxed[]).map((box) => ({ box, w: 200, h: 200 })),
  ].map(({ box, w, h }) => {
    const left = box.dx ?? 0;
    const top = box.dy ?? 0;
    return {
      left,
      top,
      right: left + (box.width || w),
      bottom: top + (box.height || h),
    };
  });

  if (boxes.length === 0) {
    return <span className="en-thumb" aria-hidden="true" />;
  }

  const left = Math.min(...boxes.map((box) => box.left));
  const top = Math.min(...boxes.map((box) => box.top));
  const width = Math.max(...boxes.map((box) => box.right)) - left;
  const height = Math.max(...boxes.map((box) => box.bottom)) - top;
  // Fitted whole inside the picture; a small key viewer is not blown up.
  const scale = Math.min(
    1,
    (THUMBNAIL_SIZE.width - MARGIN * 2) / width,
    (THUMBNAIL_SIZE.height - MARGIN * 2) / height,
  );
  const scene: CSSProperties = {
    left: (THUMBNAIL_SIZE.width - width * scale) / 2,
    top: (THUMBNAIL_SIZE.height - height * scale) / 2,
    transform: `scale(${scale})`,
  };

  return (
    <span className="en-thumb" aria-hidden="true">
      <span
        className="en-thumb-scene"
        style={scene}
        data-dmn-viewer-surface
        inert
      >
        {/* Elements place themselves from the canvas origin. */}
        <span className="en-thumb-origin" style={{ left: -left, top: -top }}>
          {shownKeys.map(({ position, index }) => (
            <StillKey
              key={index}
              position={{ ...position, zIndex: position.zIndex ?? index }}
              label={getKeyInfoByGlobalKey(keys?.[index] ?? '').displayName}
            />
          ))}
          {stats.map((position, index) => {
            const statType = (position.statType as string) ?? 'kps';
            const label =
              ((position.displayText as string) || '').trim() ||
              STAT_LABELS[statType] ||
              'KPS';
            return (
              <StatItem
                key={`stat-${index}`}
                statType={statType}
                label={label}
                position={
                  {
                    ...position,
                    zIndex: position.zIndex ?? index,
                  } as never
                }
                counterEnabled
              />
            );
          })}
          {graphs.map((position, index) => (
            <GraphPanel
              key={`graph-${index}`}
              visualPosition={position as never}
              dx={position.dx ?? 0}
              dy={position.dy ?? 0}
              width={position.width || 200}
              height={position.height || 100}
              zIndex={position.zIndex ?? index}
              className={position.className as string | undefined}
              graphType={(position.graphType as 'line' | 'bar') ?? 'line'}
              graphColor={(position.graphColor as string) || '#86EFAC'}
              showAvgLine={position.showAvgLine as boolean | undefined}
              backgroundColor={position.backgroundColor as string | undefined}
              borderColor={position.borderColor as string | undefined}
              borderWidth={position.borderWidth as number | undefined}
              borderRadius={position.borderRadius as number | undefined}
              useInlineStyles={position.useInlineStyles as boolean | undefined}
              animationEnabled={false}
              history={GRAPH_HISTORY}
              avg={12}
              maxval={18}
              uid={`thumb-${viewerId}-${index}`}
              interactive={false}
            />
          ))}
          {knobs.map((position, index) => (
            <OverlayKnobItem
              key={`knob-${index}`}
              index={index}
              // No axis: the knob rests and listens to nothing.
              position={
                {
                  ...position,
                  axisId: '',
                  zIndex: position.zIndex ?? index,
                } as never
              }
            />
          ))}
          <SpriteRuntimeLayer
            sprites={sprites}
            positions={keyPositions ?? []}
            keys={NO_KEYS}
            mode={viewerId}
          />
        </span>
      </span>
    </span>
  );
}

const STAT_LABELS: Record<string, string> = {
  kpsAvg: 'AVG',
  kpsMax: 'MAX',
  total: 'Total',
};

/** A key at rest, drawn exactly as the overlay draws one that is not held. */
function StillKey({
  position,
  label,
}: {
  position: KeyPosition;
  label: string;
}) {
  const {
    keyStyle,
    imageStyle,
    textStyle,
    currentImageSrc,
    hasCurrentImage,
    imageReplaces,
    borderRingStyle,
    isTransparent,
    labelText,
  } = computeKeyElementStyles({ position, active: false, label });
  if (isTransparent) return null;

  return (
    <div
      className={`absolute ${position.className || ''}`}
      // Many small pictures: no layer of its own for each key.
      style={{ ...keyStyle, willChange: 'auto' }}
      data-state="inactive"
    >
      {borderRingStyle && <span aria-hidden="true" style={borderRingStyle} />}
      {hasCurrentImage && (
        <img
          src={currentImageSrc || ''}
          alt=""
          style={imageStyle}
          draggable={false}
        />
      )}
      {!imageReplaces && (
        <div
          className="flex items-center justify-center h-full font-bold text-safe-inline"
          style={textStyle}
        >
          {labelText}
        </div>
      )}
    </div>
  );
}

export default memo(ViewerThumbnail);
