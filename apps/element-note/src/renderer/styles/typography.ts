export const typography = {
  style: {
    // Legacy aliases remain available while application surfaces migrate to
    // the named v2 scale. Values intentionally reference the shared tokens.
    1: {
      fontSize: 'var(--ui-type-caption-size)',
      fontWeight: 'var(--ui-type-caption-weight)',
      lineHeight: 'var(--ui-type-caption-line)',
      letterSpacing: 'var(--ui-type-caption-tracking)',
    },
    2: {
      fontSize: 'var(--ui-type-label-size)',
      fontWeight: 'var(--ui-type-label-weight)',
      lineHeight: 'var(--ui-type-label-line)',
      letterSpacing: 'var(--ui-type-label-tracking)',
    },
    3: {
      fontSize: 'var(--ui-type-title-size)',
      fontWeight: 'var(--ui-type-title-weight)',
      lineHeight: 'var(--ui-type-title-line)',
      letterSpacing: 'var(--ui-type-title-tracking)',
    },
    4: {
      fontSize: 'var(--ui-type-body-size)',
      fontWeight: 'var(--ui-type-body-weight)',
      lineHeight: 'var(--ui-type-body-line)',
      letterSpacing: 'var(--ui-type-body-tracking)',
    },
  },
} as const;

export type TypographyKeys = keyof typeof typography;
