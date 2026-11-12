import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';

interface Box {
  dx?: number;
  dy?: number;
  width?: number;
  height?: number;
  hidden?: boolean;
  backgroundColor?: unknown;
  borderRadius?: number;
}

const EMPTY: Box[] = [];
const PADDING = 12;

/** A color that can be drawn as is; anything else falls back to the default. */
const plainColor = (value: unknown): string | null =>
  typeof value === 'string' && value !== 'transparent' && value.trim() !== ''
    ? value
    : null;

/**
 * A small picture of a key viewer: where its keys and other elements are.
 * It makes each key viewer recognisable at a glance (four boxes for 4K,
 * sixteen for 16K) without its name.
 */
export default function ViewerPreview({ viewerId }: { viewerId: string }) {
  const keys = useKeyStore((state) => state.positions[viewerId]) ?? EMPTY;
  const stats = useStatItemStore((state) => state.positions[viewerId]) ?? EMPTY;
  const graphs =
    useGraphItemStore((state) => state.positions[viewerId]) ?? EMPTY;
  const knobs = useKnobItemStore((state) => state.positions[viewerId]) ?? EMPTY;

  const shapes = [
    ...(keys as Box[]).map((box) => ({ box, kind: 'key', w: 60, h: 60 })),
    ...(stats as Box[]).map((box) => ({ box, kind: 'stat', w: 60, h: 60 })),
    ...(graphs as Box[]).map((box) => ({ box, kind: 'graph', w: 200, h: 100 })),
    ...(knobs as Box[]).map((box) => ({ box, kind: 'knob', w: 60, h: 60 })),
  ]
    .filter(({ box }) => box && !box.hidden)
    .map(({ box, kind, w, h }) => ({
      kind,
      x: box.dx ?? 0,
      y: box.dy ?? 0,
      width: box.width || w,
      height: box.height || h,
      color: plainColor(box.backgroundColor),
      radius: box.borderRadius ?? 6,
    }));

  if (shapes.length === 0) {
    return <svg className="en-viewer-preview" aria-hidden="true" />;
  }

  const minX = Math.min(...shapes.map((shape) => shape.x));
  const minY = Math.min(...shapes.map((shape) => shape.y));
  const maxX = Math.max(...shapes.map((shape) => shape.x + shape.width));
  const maxY = Math.max(...shapes.map((shape) => shape.y + shape.height));

  return (
    <svg
      className="en-viewer-preview"
      viewBox={`${minX - PADDING} ${minY - PADDING} ${maxX - minX + PADDING * 2} ${maxY - minY + PADDING * 2}`}
      preserveAspectRatio="xMidYMid meet"
      aria-hidden="true"
    >
      {shapes.map((shape, index) => (
        <rect
          key={index}
          x={shape.x}
          y={shape.y}
          width={shape.width}
          height={shape.height}
          rx={
            shape.kind === 'knob'
              ? Math.min(shape.width, shape.height) / 2
              : Math.min(shape.radius, shape.width / 2, shape.height / 2)
          }
          fill={shape.color ?? 'currentColor'}
          fillOpacity={shape.color ? 1 : shape.kind === 'key' ? 0.55 : 0.3}
        />
      ))}
    </svg>
  );
}
