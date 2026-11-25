import type { CounterAnimationBezier } from '@src/types/key/keys';

export const COUNTER_DEFAULT_BEZIER: CounterAnimationBezier = [
  0.25, 0.46, 0.45, 0.94,
];

export type CounterBezierPresetId =
  'current' | 'linear' | 'easeOut' | 'easeIn' | 'easeInOut' | 'overshoot';

export interface CounterBezierPreset {
  id: CounterBezierPresetId;
  fallbackLabel: string;
  bezier: CounterAnimationBezier;
}

export const COUNTER_BEZIER_PRESETS: CounterBezierPreset[] = [
  {
    id: 'current',
    fallbackLabel: 'Default',
    bezier: [0.25, 0.46, 0.45, 0.94],
  },
  {
    id: 'linear',
    fallbackLabel: 'Linear',
    bezier: [0, 0, 1, 1],
  },
  {
    id: 'easeOut',
    fallbackLabel: 'Ease Out',
    bezier: [0, 0, 0.58, 1],
  },
  {
    id: 'easeIn',
    fallbackLabel: 'Ease In',
    bezier: [0.42, 0, 1, 1],
  },
  {
    id: 'easeInOut',
    fallbackLabel: 'Ease In-Out',
    bezier: [0.42, 0, 0.58, 1],
  },
  {
    id: 'overshoot',
    fallbackLabel: 'Overshoot',
    bezier: [0.34, 1.56, 0.64, 1],
  },
];

const A = (a1: number, a2: number) => 1 - 3 * a2 + 3 * a1;
const B = (a1: number, a2: number) => 3 * a2 - 6 * a1;
const C = (a1: number) => 3 * a1;

const calcBezier = (t: number, a1: number, a2: number) =>
  ((A(a1, a2) * t + B(a1, a2)) * t + C(a1)) * t;

const getSlope = (t: number, a1: number, a2: number) =>
  3 * A(a1, a2) * t * t + 2 * B(a1, a2) * t + C(a1);

const solveTForX = (x: number, x1: number, x2: number) => {
  let t = x;

  for (let i = 0; i < 8; i += 1) {
    const slope = getSlope(t, x1, x2);
    if (Math.abs(slope) < 1e-6) break;
    const currentX = calcBezier(t, x1, x2) - x;
    t -= currentX / slope;
  }

  let lower = 0;
  let upper = 1;

  if (t < lower || t > upper || Number.isNaN(t)) {
    t = x;
  }

  for (let i = 0; i < 12; i += 1) {
    const currentX = calcBezier(t, x1, x2);
    if (Math.abs(currentX - x) < 1e-6) {
      return t;
    }
    if (currentX < x) {
      lower = t;
    } else {
      upper = t;
    }
    t = (lower + upper) / 2;
  }

  return t;
};

export const clampCounterBezier = (
  bezier: CounterAnimationBezier | number[],
): CounterAnimationBezier => {
  return [
    Math.min(Math.max(Number(bezier?.[0] ?? COUNTER_DEFAULT_BEZIER[0]), 0), 1),
    Math.min(Math.max(Number(bezier?.[1] ?? COUNTER_DEFAULT_BEZIER[1]), -4), 4),
    Math.min(Math.max(Number(bezier?.[2] ?? COUNTER_DEFAULT_BEZIER[2]), 0), 1),
    Math.min(Math.max(Number(bezier?.[3] ?? COUNTER_DEFAULT_BEZIER[3]), -4), 4),
  ];
};

export const createCubicBezierEasing = (
  bezier: CounterAnimationBezier | number[] | undefined,
): ((t: number) => number) => {
  const [x1, y1, x2, y2] = clampCounterBezier(bezier ?? COUNTER_DEFAULT_BEZIER);

  return (t: number) => {
    const x = Math.min(Math.max(t, 0), 1);
    if (x === 0) return 0;
    if (x === 1) return 1;
    const solvedT = solveTForX(x, x1, x2);
    return calcBezier(solvedT, y1, y2);
  };
};

export const bezierToCssString = (
  bezier: CounterAnimationBezier | number[],
): string => {
  const [x1, y1, x2, y2] = clampCounterBezier(bezier);
  return `cubic-bezier(${x1.toFixed(2)}, ${y1.toFixed(2)}, ${x2.toFixed(
    2,
  )}, ${y2.toFixed(2)})`;
};

export const findBezierPresetId = (
  bezier: CounterAnimationBezier | number[],
): CounterBezierPresetId | 'custom' => {
  const target = clampCounterBezier(bezier);
  const matched = COUNTER_BEZIER_PRESETS.find((preset) => {
    return preset.bezier.every((value, index) => {
      const targetValue = target[index as 0 | 1 | 2 | 3];
      return Math.abs(Number(value) - Number(targetValue)) < 0.001;
    });
  });
  return matched ? matched.id : 'custom';
};

/** The curves the CSS easing keywords stand for. */
const CSS_EASING_KEYWORDS: Record<string, CounterAnimationBezier> = {
  linear: [0, 0, 1, 1],
  ease: [0.25, 0.1, 0.25, 1],
  'ease-in': [0.42, 0, 1, 1],
  'ease-out': [0, 0, 0.58, 1],
  'ease-in-out': [0.42, 0, 0.58, 1],
};

const CSS_NUMBER = String.raw`[+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?`;
const CSS_CUBIC_BEZIER = new RegExp(
  String.raw`^cubic-bezier\(\s*(${CSS_NUMBER})\s*,\s*(${CSS_NUMBER})\s*,\s*(${CSS_NUMBER})\s*,\s*(${CSS_NUMBER})\s*\)$`,
  'i',
);

/**
 * The curve a CSS easing is: a keyword or `cubic-bezier(…)`, however it is
 * spelled. Null for anything else (steps, `linear(…)`, text that is no easing).
 */
export const cssEasingToBezier = (
  easing: string,
): CounterAnimationBezier | null => {
  const text = easing.trim().toLowerCase();
  const keyword = CSS_EASING_KEYWORDS[text];
  if (keyword) return keyword;
  const match = CSS_CUBIC_BEZIER.exec(text);
  if (!match) return null;
  const curve = match.slice(1, 5).map(Number) as CounterAnimationBezier;
  return curve.every(Number.isFinite) ? curve : null;
};

/** Two curves that move the same way, whatever digits they were written with. */
export const isSameBezier = (
  a: CounterAnimationBezier,
  b: CounterAnimationBezier,
): boolean => a.every((value, index) => Math.abs(value - b[index]) < 0.001);
