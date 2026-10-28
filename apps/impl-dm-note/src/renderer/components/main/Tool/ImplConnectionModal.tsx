import { useMemo } from 'react';
import Modal from '../Modal/Modal';
import Toggle from '../common/Toggle';
import { useTranslation } from '@contexts/useTranslation';
import { useKeyLimiterStatusStore } from '@stores/useKeyLimiterStatusStore';
import { useSettingsStore } from '@stores/useSettingsStore';
import type { StatusPresentation } from './keyLimiterStatusPresentation';

interface ImplConnectionModalProps {
  presentation: StatusPresentation;
  onClose: () => void;
}

type ConnectionSetting = 'keyLimiterEnabled' | 'inGameViewersEnabled';

const CloseIcon = () => (
  <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true">
    <path
      d="m2.5 2.5 7 7m0-7-7 7"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
    />
  </svg>
);

/** ImplResourcePack link: what the app sends the game, and what it applied. */
export default function ImplConnectionModal({
  presentation,
  onClose,
}: ImplConnectionModalProps) {
  const { t, i18n } = useTranslation();
  const acknowledgement = useKeyLimiterStatusStore(
    (state) => state.lastAcknowledgement,
  );
  const error = useKeyLimiterStatusStore((state) => state.error);
  const keyLimiterEnabled = useSettingsStore(
    (state) => state.keyLimiterEnabled,
  );
  const inGameViewersEnabled = useSettingsStore(
    (state) => state.inGameViewersEnabled,
  );
  const unsupportedKeys = acknowledgement?.unsupportedKeys ?? [];
  const appliedAt = useMemo(() => {
    if (!acknowledgement) return null;
    return new Intl.DateTimeFormat(i18n.language, {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    }).format(acknowledgement.acknowledgedAt);
  }, [acknowledgement, i18n.language]);
  // The title already names ImplResourcePack; the status line says only the state.
  const status =
    presentation.labelKey === 'keyLimiter.connected'
      ? t('implConnection.connected')
      : t(presentation.labelKey);

  // Optimistic: the store follows the switch at once and rolls back if the
  // backend refuses the change.
  const toggle = (setting: ConnectionSetting, current: boolean) => {
    const next = !current;
    useSettingsStore.getState().merge({ [setting]: next });
    window.api.settings.update({ [setting]: next }).catch((reason) => {
      console.error(`Failed to update ${setting}`, reason);
      useSettingsStore.getState().merge({ [setting]: current });
    });
  };

  const rows: Array<{
    setting: ConnectionSetting;
    checked: boolean;
    label: string;
    description: string;
  }> = [
    {
      setting: 'keyLimiterEnabled',
      checked: keyLimiterEnabled,
      label: t('implConnection.keyLimiter'),
      description: t('implConnection.keyLimiterDescription'),
    },
    {
      setting: 'inGameViewersEnabled',
      checked: inGameViewersEnabled,
      label: t('implConnection.inGame'),
      description: t('implConnection.inGameDescription'),
    },
  ];

  return (
    <Modal onClick={onClose} ariaLabel={t('implConnection.title')}>
      <div
        className="dmn-connection-modal"
        onClick={(event) => event.stopPropagation()}
      >
        <header className="dmn-connection-modal__header">
          <div className="dmn-connection-modal__heading">
            <h2>{t('implConnection.title')}</h2>
            <p
              className={`dmn-connection-modal__status dmn-connection-modal__status--${presentation.tone}`}
            >
              {status}
            </p>
          </div>
          <button
            type="button"
            className="dmn-connection-modal__close"
            aria-label={t('implConnection.close')}
            onClick={onClose}
          >
            <CloseIcon />
          </button>
        </header>

        <div className="dmn-connection-modal__group">
          {rows.map((row) => (
            <div key={row.setting} className="dmn-connection-modal__row">
              <div className="dmn-connection-modal__copy">
                <p className="dmn-connection-modal__label">{row.label}</p>
                <p className="dmn-connection-modal__description">
                  {row.description}
                </p>
              </div>
              <Toggle
                checked={row.checked}
                ariaLabel={row.label}
                onChange={() => toggle(row.setting, row.checked)}
              />
            </div>
          ))}
        </div>

        {acknowledgement ? (
          <dl className="dmn-connection-modal__stats">
            <div>
              <dt>{t('keyLimiter.profile')}</dt>
              <dd title={acknowledgement.profile.name}>
                {acknowledgement.profile.name}
              </dd>
            </div>
            <div>
              <dt>{t('implConnection.keys')}</dt>
              <dd>{acknowledgement.supportedKeys.length}</dd>
            </div>
            <div>
              <dt>{t('implConnection.updated')}</dt>
              <dd>{appliedAt}</dd>
            </div>
          </dl>
        ) : (
          <p className="dmn-connection-modal__note">
            {t('keyLimiter.waitingDetail')}
          </p>
        )}
        {unsupportedKeys.length > 0 && (
          <p className="dmn-connection-modal__note dmn-connection-modal__note--warning">
            {t('keyLimiter.unsupportedKeys')}: {unsupportedKeys.join(', ')}
          </p>
        )}
        {error && (
          <p className="dmn-connection-modal__note dmn-connection-modal__note--error">
            {error}
          </p>
        )}
      </div>
    </Modal>
  );
}
