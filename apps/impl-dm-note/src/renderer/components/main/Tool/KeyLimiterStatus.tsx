import { useState } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { useKeyLimiterStatusStore } from '@stores/useKeyLimiterStatusStore';
import ImplConnectionModal from './ImplConnectionModal';
import {
  getKeyLimiterStatusPresentation,
  type StatusIcon,
} from './keyLimiterStatusPresentation';

function StatusGlyph({ icon }: { icon: StatusIcon }) {
  if (icon === 'check') {
    return (
      <svg
        className="key-limiter-status-glyph key-limiter-status-glyph--complete"
        viewBox="0 0 16 16"
        aria-hidden="true"
      >
        <path
          d="m4.1 8.2 2.35 2.35L11.9 5.1"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    );
  }
  if (icon === 'warning') {
    return (
      <svg
        className="key-limiter-status-glyph"
        viewBox="0 0 16 16"
        aria-hidden="true"
      >
        <path
          d="M8 2.4 14 13H2L8 2.4Z"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.35"
          strokeLinejoin="round"
        />
        <path
          d="M8 6v3.2M8 11.5v.1"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
        />
      </svg>
    );
  }
  if (icon === 'error') {
    return (
      <svg
        className="key-limiter-status-glyph"
        viewBox="0 0 16 16"
        aria-hidden="true"
      >
        <circle
          cx="8"
          cy="8"
          r="5.5"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.35"
        />
        <path
          d="m6 6 4 4m0-4-4 4"
          stroke="currentColor"
          strokeWidth="1.45"
          strokeLinecap="round"
        />
      </svg>
    );
  }
  if (icon === 'progress') {
    return (
      <svg
        className="key-limiter-status-glyph key-limiter-status-glyph--progress"
        viewBox="0 0 16 16"
        aria-hidden="true"
      >
        <circle
          cx="8"
          cy="8"
          r="5.25"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
          strokeDasharray="23 10"
        />
      </svg>
    );
  }
  return null;
}

export default function KeyLimiterStatus() {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const connectionState = useKeyLimiterStatusStore(
    (state) => state.connectionState,
  );
  const deliveryState = useKeyLimiterStatusStore(
    (state) => state.deliveryState,
  );
  const hasUnsupportedKeys = useKeyLimiterStatusStore(
    (state) => (state.lastAcknowledgement?.unsupportedKeys.length ?? 0) > 0,
  );
  const presentation = getKeyLimiterStatusPresentation(
    connectionState,
    deliveryState,
    hasUnsupportedKeys,
  );
  const label = t(presentation.labelKey);

  return (
    <>
      <button
        type="button"
        className={`key-limiter-status key-limiter-status--${presentation.tone}`}
        aria-label={label}
        aria-haspopup="dialog"
        aria-expanded={isOpen}
        onClick={() => setIsOpen(true)}
      >
        <span
          className="key-limiter-status__live"
          role="status"
          aria-live="polite"
          aria-atomic="true"
        >
          <StatusGlyph icon={presentation.icon} />
          <span className="key-limiter-status__label">{label}</span>
        </span>
      </button>
      {isOpen && (
        <ImplConnectionModal
          presentation={presentation}
          onClose={() => setIsOpen(false)}
        />
      )}
    </>
  );
}
