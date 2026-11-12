import { useEffect, useState } from 'react';
import { getCounterCacheSnapshot } from '@stores/signals/keyCounterCache';
import { SettingsGroup } from './FeatureSettings';
import { NumberInput, PropertyRow } from './PropertyInputs';

/** The most a count can be: what the app keeps a count in (u32). */
const MAX_COUNT = 4_294_967_295;

interface KeyCountSettingsProps {
  /** The key viewer and the keyboard key the count is of. */
  mode: string;
  keyCode: string;
  t: (key: string) => string;
}

const storedCount = (mode: string, keyCode: string) =>
  getCounterCacheSnapshot()[mode]?.[keyCode] ?? 0;

/**
 * How many times the key has been pressed, at the foot of its count's page:
 * it goes up while the key is pressed, and it can be changed by hand (0
 * resets it). Changing it goes into undo like any other change of counts.
 */
export default function KeyCountSettings({
  mode,
  keyCode,
  t,
}: KeyCountSettingsProps) {
  const [count, setCount] = useState(() => storedCount(mode, keyCode));
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    setCount(storedCount(mode, keyCode));
    const offOne = window.api.keys.onCounterChanged((update) => {
      if (update.mode === mode && update.key === keyCode)
        setCount(update.count);
    });
    const offAll = window.api.keys.onCountersChanged((counters) => {
      const next = counters[mode]?.[keyCode];
      if (typeof next === 'number') setCount(next);
    });
    return () => {
      offOne();
      offAll();
    };
  }, [mode, keyCode]);

  const save = async (value: number) => {
    if (value === count) return;
    setFailed(false);
    try {
      await window.api.keys.setSingleCounter(mode, keyCode, value);
      setCount(value);
    } catch (error) {
      console.error('Failed to set the key count', error);
      setFailed(true);
      // The field shows the count as it still is.
      setCount(storedCount(mode, keyCode));
    }
  };

  return (
    <SettingsGroup id="counter-count" title={t('keyCount.label')}>
      <PropertyRow label={t('keyCount.now')}>
        <NumberInput
          ariaLabel={t('keyCount.label')}
          value={count}
          min={0}
          max={MAX_COUNT}
          width="96px"
          onChange={(value) => void save(value)}
        />
      </PropertyRow>
      {failed && (
        <p className="en-section-description" role="alert">
          {t('keyCount.failed')}
        </p>
      )}
    </SettingsGroup>
  );
}
