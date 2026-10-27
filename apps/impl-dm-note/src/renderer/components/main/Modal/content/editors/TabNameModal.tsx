import React, { useEffect, useState } from 'react';
import Modal from '../../Modal';
import { useTranslation } from '@contexts/useTranslation';
import Button from '@components/main/common/Button';

interface TabNameModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (
    name: string,
  ) => Promise<{ error?: string } | void> | { error?: string } | void;
  existingNames?: string[];
  initialName?: string;
  mode?: 'create' | 'rename';
}

const TabNameModal = ({
  isOpen,
  onClose,
  onSubmit,
  existingNames = [],
  initialName = '',
  mode = 'create',
}: TabNameModalProps) => {
  const { t } = useTranslation();
  const [name, setName] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setName(initialName);
      setError(null);
    }
  }, [initialName, isOpen]);

  const validate = (() => {
    return (v: string) => {
      if (!v || !v.trim()) return t('tabs.name.required');
      if (v.length > 10) return t('tabs.name.max');
      if (existingNames.includes(v)) return t('tabs.name.duplicate');
      return null;
    };
  })();

  const handleSubmit = async () => {
    const err = validate(name.trim());
    if (err) {
      setError(err);
      return;
    }
    const res = await onSubmit(name.trim());
    if (res && typeof res === 'object' && 'error' in res && res.error) {
      const map: Record<string, string> = {
        'max-reached': t('tabs.errors.max'),
        'duplicate-name': t('tabs.name.duplicate'),
        'invalid-name': t('tabs.errors.invalid'),
        'name-too-long': t('tabs.name.max'),
        'not-found': t('tabs.errors.notFound'),
      };
      setError(map[res.error] || t('tabs.errors.createFail'));
      return;
    }
    onClose();
  };

  if (!isOpen) return null;

  return (
    <Modal onClick={onClose}>
      <div
        className="dmn-modal-card flex flex-col justify-between w-[280px] p-[20px] gap-[19px]"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="text-style-3 text-[#FFFFFF]">
          {t(mode === 'rename' ? 'tabs.renameTitle' : 'tabs.createTitle')}
        </div>
        <input
          autoFocus
          value={name}
          onChange={(e) => setName(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') handleSubmit();
          }}
          className="dmn-text-field w-full min-w-0 h-[30px] px-[12px] text-style-3"
          placeholder={t('tabs.name.placeholder')}
        />
        {error && (
          <div className="text-[#ED6A5E] text-style-1 my-[-12px]">{error}</div>
        )}
        <div className="flex gap-[10.5px]">
          <Button variant="primary" className="flex-1" onClick={handleSubmit}>
            {t(mode === 'rename' ? 'tabs.rename' : 'tabs.create')}
          </Button>
          <Button variant="danger" className="w-[75px]" onClick={onClose}>
            {t('common.cancel')}
          </Button>
        </div>
      </div>
    </Modal>
  );
};

export default TabNameModal;
