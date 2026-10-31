import { useRef } from 'react';
import Modal from '../Modal/Modal';
import Button from '../common/Button';
import { useTranslation } from '@contexts/useTranslation';

interface ObsModePromptProps {
  onEnable: () => void;
  onDismiss: () => void;
}

/** Asks once per run to turn on OBS mode so the game can show key viewers. */
export default function ObsModePrompt({
  onEnable,
  onDismiss,
}: ObsModePromptProps) {
  const { t } = useTranslation();
  const enableRef = useRef<HTMLButtonElement>(null);

  return (
    <Modal
      onClick={onDismiss}
      ariaLabel={t('obsModePrompt.title')}
      initialFocusRef={enableRef}
    >
      <div
        className="dmn-modal-card dmn-obs-prompt"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dmn-obs-prompt__copy">
          <h2>{t('obsModePrompt.title')}</h2>
          <p>{t('obsModePrompt.message')}</p>
        </div>
        <div className="dmn-obs-prompt__actions">
          <Button variant="secondary" onClick={onDismiss}>
            {t('obsModePrompt.dismiss')}
          </Button>
          <Button ref={enableRef} variant="primary" onClick={onEnable}>
            {t('obsModePrompt.enable')}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
