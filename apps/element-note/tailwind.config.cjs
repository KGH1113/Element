/** @type {import('tailwindcss').Config} */
const { typography } = require('./src/renderer/styles/typography');
const { colors } = require('./src/renderer/styles/colors');

module.exports = {
  content: ['./src/**/*.{js,jsx,tsx,html}'],
  theme: {
    extend: {
      colors: {
        ui: {
          app: 'var(--ui-bg-app)',
          panel: 'var(--ui-bg-panel)',
          'panel-detached': 'var(--ui-bg-panel-detached)',
          surface: 'var(--ui-bg-surface)',
          'surface-hover': 'var(--ui-bg-surface-hover)',
          'surface-active': 'var(--ui-bg-surface-active)',
          elevated: 'var(--ui-bg-elevated)',
          inset: 'var(--ui-bg-inset)',
          'inset-solid': 'var(--ui-bg-inset-solid)',
          modal: 'var(--ui-bg-modal)',
          glass: 'var(--ui-bg-glass)',
          'glass-dim': 'var(--ui-glass-dim)',
          scrim: 'var(--ui-scrim)',
          fill: {
            DEFAULT: 'var(--ui-fill)',
            hover: 'var(--ui-fill-hover)',
            active: 'var(--ui-fill-active)',
            faint: 'var(--ui-fill-faint)',
          },
          line: {
            faint: 'var(--ui-line-faint)',
            subtle: 'var(--ui-line-subtle)',
            DEFAULT: 'var(--ui-line)',
            strong: 'var(--ui-line-strong)',
          },
          selection: {
            DEFAULT: 'var(--ui-selection)',
            border: 'var(--ui-selection-border)',
            'border-strong': 'var(--ui-selection-border-strong)',
            fill: 'var(--ui-selection-fill)',
            glow: 'var(--ui-selection-glow)',
          },
          fg: {
            primary: 'var(--ui-fg-primary)',
            secondary: 'var(--ui-fg-secondary)',
            muted: 'var(--ui-fg-muted)',
            disabled: 'var(--ui-fg-disabled)',
            inverse: 'var(--ui-fg-inverse)',
          },
          accent: {
            DEFAULT: 'var(--ui-accent)',
            hover: 'var(--ui-accent-hover)',
            pressed: 'var(--ui-accent-pressed)',
            soft: 'var(--ui-accent-soft)',
            fg: 'var(--ui-accent-fg)',
            text: 'var(--ui-accent-text)',
          },
          success: 'var(--ui-success)',
          warning: 'var(--ui-warning)',
          danger: 'var(--ui-danger)',
        },
        primary: colors.primary,
        button: colors.button,
        text: colors.text,
        border: colors.border,
        surface: colors.surface,
        surfaceHover: colors.surfaceHover,
        surfaceActive: colors.surfaceActive,
        hoverDark: colors.hoverDark,
        focus: colors.focus,
        textDisabled: colors.textDisabled,
        danger: colors.danger,
      },
      borderRadius: {
        'ui-inner': 'var(--ui-radius-inner)',
        'ui-control': 'var(--ui-radius-control)',
        'ui-surface': 'var(--ui-radius-surface)',
        'ui-popup': 'var(--ui-radius-popup)',
        'ui-modal': 'var(--ui-radius-modal)',
      },
      boxShadow: {
        'ui-surface': 'var(--ui-shadow-surface)',
        'ui-popup': 'var(--ui-shadow-popup)',
        'ui-modal': 'var(--ui-shadow-modal)',
        'ui-chrome': 'var(--ui-shadow-chrome)',
        'ui-tooltip': 'var(--ui-shadow-tooltip)',
        'ui-focus': 'var(--ui-shadow-focus)',
        'ui-focus-ring': 'var(--ui-focus-ring)',
      },
    },
  },
  plugins: [
    function ({ addUtilities }) {
      addUtilities({
        '.all-unset': {
          all: 'unset',
        },
        '.text-caption': {
          fontSize: 'var(--ui-type-caption-size)',
          lineHeight: 'var(--ui-type-caption-line)',
          letterSpacing: 'var(--ui-type-caption-tracking)',
          fontWeight: 'var(--ui-type-caption-weight)',
        },
        '.text-body': {
          fontSize: 'var(--ui-type-body-size)',
          lineHeight: 'var(--ui-type-body-line)',
          letterSpacing: 'var(--ui-type-body-tracking)',
          fontWeight: 'var(--ui-type-body-weight)',
        },
        '.text-label': {
          fontSize: 'var(--ui-type-label-size)',
          lineHeight: 'var(--ui-type-label-line)',
          letterSpacing: 'var(--ui-type-label-tracking)',
          fontWeight: 'var(--ui-type-label-weight)',
        },
        '.text-title': {
          fontSize: 'var(--ui-type-title-size)',
          lineHeight: 'var(--ui-type-title-line)',
          letterSpacing: 'var(--ui-type-title-tracking)',
          fontWeight: 'var(--ui-type-title-weight)',
        },
        '.text-heading': {
          fontSize: 'var(--ui-type-heading-size)',
          lineHeight: 'var(--ui-type-heading-line)',
          letterSpacing: 'var(--ui-type-heading-tracking)',
          fontWeight: 'var(--ui-type-heading-weight)',
        },
        '.text-style-1': {
          fontSize: typography.style[1].fontSize,
          lineHeight: typography.style[1].lineHeight,
          letterSpacing: typography.style[1].letterSpacing,
          fontWeight: typography.style[1].fontWeight,
        },
        '.text-style-2': {
          fontSize: typography.style[2].fontSize,
          lineHeight: typography.style[2].lineHeight,
          letterSpacing: typography.style[2].letterSpacing,
          fontWeight: typography.style[2].fontWeight,
        },
        '.text-style-3': {
          fontSize: typography.style[3].fontSize,
          lineHeight: typography.style[3].lineHeight,
          letterSpacing: typography.style[3].letterSpacing,
          fontWeight: typography.style[3].fontWeight,
        },
        '.text-style-4': {
          fontSize: typography.style[4].fontSize,
          lineHeight: typography.style[4].lineHeight,
          letterSpacing: typography.style[4].letterSpacing,
          fontWeight: typography.style[4].fontWeight,
        },
      });
    },
  ],
};
