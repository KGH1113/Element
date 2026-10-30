import React, { useState } from 'react';
import Modal from '../../Modal';
import Checkbox from '@components/main/common/Checkbox';
import { useTranslation } from '@contexts/useTranslation';
import {
  clampValue,
  NOTE_SETTINGS_CONSTRAINTS,
} from '@src/types/settings/noteSettingsConstraints';

interface LaboratoryProps {
  delayEnabled: boolean;
  thresholdMs: number;
  minLengthPx: number;
  keyDisplayDelayMs: number;
  trackHeight: number;
  speed: number;
  onSave: (payload: {
    delayedNoteEnabled: boolean;
    shortNoteThresholdMs: number;
    shortNoteMinLengthPx: number;
    keyDisplayDelayMs: number;
  }) => Promise<void> | void;
  onClose: () => void;
}

const LaboratoryModal = ({
  delayEnabled,
  thresholdMs,
  minLengthPx,
  keyDisplayDelayMs,
  trackHeight,
  speed,
  onSave,
  onClose,
}: LaboratoryProps) => {
  const { t } = useTranslation();
  const [enforceShort, setEnforceShort] = useState(!!delayEnabled);
  // store as strings so the user can clear the input while typing
  const [threshold, setThreshold] = useState(String(thresholdMs ?? 120));
  const [minimum, setMinimum] = useState(String(minLengthPx ?? 10));
  const [keyDelay, setKeyDelay] = useState(String(keyDisplayDelayMs ?? 0));

  // 자동 계산된 키 딜레이 값 (노트가 키에 도달하는 시간)
  const parsedThreshold = (() => {
    const value = Number(threshold);
    if (!Number.isFinite(value) || value < 0) return 0;
    return Math.min(value, 2000);
  })();

  const calculatedDelay = (() => {
    if (!speed || speed <= 0) return 0;
    const travelDelay = Math.round((trackHeight / speed) * 1000);
    const shortNoteDelay = enforceShort ? parsedThreshold : 0;
    return travelDelay + shortNoteDelay;
  })();

  const handleAutoCalculate = () => {
    setKeyDelay(String(clampValue(calculatedDelay, 'keyDisplayDelayMs')));
  };

  const handleSave = async () => {
    const sanitizedThreshold = Math.max(
      0,
      Math.min(parseInt(String(threshold), 10) || 0, 2000),
    );
    const sanitizedMinimum = Math.max(
      1,
      Math.min(parseInt(String(minimum), 10) || 1, 100),
    );
    const sanitizedKeyDelay = clampValue(
      parseInt(String(keyDelay), 10) || 0,
      'keyDisplayDelayMs',
    );

    try {
      await onSave({
        delayedNoteEnabled: enforceShort,
        shortNoteThresholdMs: sanitizedThreshold,
        shortNoteMinLengthPx: sanitizedMinimum,
        keyDisplayDelayMs: sanitizedKeyDelay,
      });
    } finally {
      onClose();
    }
  };

  return (
    <Modal onClick={onClose}>
      <div
        className="dmn-modal-card flex flex-col items-center justify-center p-[20px] gap-[19px]"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex justify-between w-full items-center">
          <p className="text-white text-style-2">
            {t('laboratory.delayToggle')}
          </p>
          <Checkbox
            checked={enforceShort}
            onChange={() => setEnforceShort((prev) => !prev)}
          />
        </div>

        <div className="flex justify-between w-full items-center">
          <p className="text-white text-style-2">{t('laboratory.minLength')}</p>
          <input
            type="number"
            min={1}
            max={100}
            value={minimum}
            onChange={(e) => setMinimum(e.target.value)}
            onBlur={() => {
              const sanitized = Math.max(
                1,
                Math.min(Number(minimum) || 1, 100),
              );
              setMinimum(String(sanitized));
            }}
            className="text-center w-[47px] h-[23px] bg-[#2A2A30] rounded-[7px] border-[1px] border-[#3A3943] focus:border-ui-accent text-style-4 text-[#DBDEE8]"
          />
        </div>

        <div className="flex justify-between w-full items-center">
          <p className="text-white text-style-2">{t('laboratory.threshold')}</p>
          <input
            type="number"
            min={0}
            max={2000}
            value={threshold}
            onChange={(e) => setThreshold(e.target.value)}
            onBlur={() => {
              const sanitized = Math.max(
                0,
                Math.min(Number(threshold) || 0, 2000),
              );
              setThreshold(String(sanitized));
            }}
            className="text-center w-[47px] h-[23px] bg-[#2A2A30] rounded-[7px] border-[1px] border-[#3A3943] focus:border-ui-accent text-style-4 text-[#DBDEE8]"
          />
        </div>

        {/* 구분선 */}
        <div className="w-full h-[1px] bg-[#2A2A30]" />

        {/* 키 딜레이 설정 */}
        <div className="flex justify-between w-full items-center">
          <p className="text-white text-style-2">{t('laboratory.keyDelay')}</p>
          <div className="flex items-center gap-[8px]">
            <input
              type="number"
              min={NOTE_SETTINGS_CONSTRAINTS.keyDisplayDelayMs.min}
              max={NOTE_SETTINGS_CONSTRAINTS.keyDisplayDelayMs.max}
              value={keyDelay}
              onChange={(e) => setKeyDelay(e.target.value)}
              onBlur={() => {
                const sanitized = clampValue(
                  Number(keyDelay) || 0,
                  'keyDisplayDelayMs',
                );
                setKeyDelay(String(sanitized));
              }}
              className="text-center w-[55px] h-[23px] bg-[#2A2A30] rounded-[7px] border-[1px] border-[#3A3943] focus:border-ui-accent text-style-4 text-[#DBDEE8]"
            />
          </div>
        </div>

        <div className="flex justify-between w-full items-center">
          <p className="text-[#717178] text-style-4">
            {t('laboratory.keyDelayAuto', { value: calculatedDelay })}
          </p>
          <button
            onClick={handleAutoCalculate}
            className="px-[10px] h-[23px] bg-[#2A2A30] hover:bg-[#303036] active:bg-[#393941] rounded-[7px] text-[#DCDEE7] text-style-4"
          >
            {t('laboratory.autoCalc')}
          </button>
        </div>

        <div className="flex gap-[10.5px]">
          <button
            onClick={handleSave}
            className="w-[150px] h-[30px] bg-[#2A2A30] hover:bg-[#303036] active:bg-[#393941] rounded-[7px] text-[#DCDEE7] text-style-3"
          >
            {t('laboratory.save')}
          </button>
          <button
            onClick={onClose}
            className="w-[75px] h-[30px] bg-[#3C1E1E] hover:bg-[#442222] active:bg-[#522929] rounded-[7px] text-[#E6DBDB] text-style-3"
          >
            {t('laboratory.cancel')}
          </button>
        </div>
      </div>
    </Modal>
  );
};

export default LaboratoryModal;
