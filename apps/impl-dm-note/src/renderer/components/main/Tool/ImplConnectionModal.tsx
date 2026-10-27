import { useMemo } from 'react';
import Modal from '../Modal/Modal';
import { SettingCard, SettingToggleRow } from '../common/SettingRow';
import { useTranslation } from '@contexts/useTranslation';
import { useKeyLimiterStatusStore } from '@stores/useKeyLimiterStatusStore';
import { useSettingsStore } from '@stores/useSettingsStore';
import type { StatusPresentation } from './keyLimiterStatusPresentation';

interface ImplConnectionModalProps {
  presentation: StatusPresentation;
  onClose: () => void;
}

type ConnectionSetting = 'keyLimiterEnabled' | 'inGameViewersEnabled';

/** ImplResourcePack link: what the game received, and what the app sends it. */
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
    }).format(acknowledgement.acknowledgedAt);
  }, [acknowledgement, i18n.language]);

  // Optimistic: the store follows the toggle at once and rolls back if the
  // backend refuses the change.
  const toggle = (setting: ConnectionSetting, current: boolean) => {
    const next = !current;
    useSettingsStore.getState().merge({ [setting]: next });
    window.api.settings.update({ [setting]: next }).catch((reason) => {
      console.error(`Failed to update ${setting}`, reason);
      useSettingsStore.getState().merge({ [setting]: current });
    });
  };

  return (
    <Modal onClick={onClose} ariaLabel={t('implConnection.title')}>
      <div
        className="dmn-connection-modal"
        onClick={(event) => event.stopPropagation()}
      >
        <header className="dmn-connection-modal__header">
          <h2>{t('implConnection.title')}</h2>
          <p
            className={`dmn-connection-modal__status key-limiter-status--${presentation.tone}`}
          >
            {t(presentation.labelKey)}
          </p>
        </header>

        <SettingCard>
          <SettingToggleRow
            label={t('implConnection.keyLimiter')}
            description={t('implConnection.keyLimiterDescription')}
            checked={keyLimiterEnabled}
            onToggle={() => toggle('keyLimiterEnabled', keyLimiterEnabled)}
          />
          <SettingToggleRow
            label={t('implConnection.inGame')}
            description={t('implConnection.inGameDescription')}
            checked={inGameViewersEnabled}
            onToggle={() =>
              toggle('inGameViewersEnabled', inGameViewersEnabled)
            }
          />
        </SettingCard>

        {acknowledgement ? (
          <dl className="dmn-connection-modal__details">
            <div>
              <dt>{t('keyLimiter.profile')}</dt>
              <dd>{acknowledgement.profile.name}</dd>
            </div>
            <div>
              <dt>{t('keyLimiter.appliedKeys')}</dt>
              <dd>{acknowledgement.supportedKeys.length}</dd>
            </div>
            <div>
              <dt>{t('keyLimiter.lastApplied')}</dt>
              <dd>{appliedAt}</dd>
            </div>
            {unsupportedKeys.length > 0 && (
              <div className="dmn-connection-modal__unsupported">
                <dt>{t('keyLimiter.unsupportedKeys')}</dt>
                <dd>{unsupportedKeys.join(', ')}</dd>
              </div>
            )}
          </dl>
        ) : (
          <p className="dmn-connection-modal__note">
            {t('keyLimiter.waitingDetail')}
          </p>
        )}
        {error && (
          <p className="dmn-connection-modal__note dmn-connection-modal__note--error">
            {error}
          </p>
        )}

        <button
          type="button"
          className="dmn-connection-modal__done"
          onClick={onClose}
        >
          {t('implConnection.done')}
        </button>
      </div>
    </Modal>
  );
}
