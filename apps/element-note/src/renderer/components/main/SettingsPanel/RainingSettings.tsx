import { useRef, useState } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useKeyStore } from '@stores/data/useKeyStore';
import {
  SettingRow,
  SettingToggleRow,
} from '@components/main/common/SettingRow';
import Button from '@components/main/common/Button';
import Dropdown from '@components/main/common/Dropdown';
import {
  NumberInput,
  type NumberInputProps,
} from '@components/main/common/NumberInput';
import {
  mergeNoteSettings,
  type NoteSettings,
  type TabNoteSettings,
} from '@src/types/settings/noteSettings';
import { ownRainingValues } from './rainingValues';
import { NOTE_SETTINGS_CONSTRAINTS as LIMITS } from '@src/types/settings/noteSettingsConstraints';

/** Every key viewer, as opposed to one of them. */
const ALL = 'all';

/**
 * The Raining values, for every key viewer or for one of them, and a way to
 * change one value. A change is kept at once.
 */
function useRaining(scope: string) {
  const shared = useSettingsStore((state) => state.noteSettings);
  const setShared = useSettingsStore((state) => state.setNoteSettings);
  const owns = useSettingsStore((state) => state.tabNoteOverrides);
  const setOwns = useSettingsStore((state) => state.setTabNoteOverrides);

  const own = scope === ALL ? null : (owns[scope] ?? null);
  const values = mergeNoteSettings(shared, own);

  const saveOwn = (next: TabNoteSettings | null) => {
    const rest = { ...owns };
    if (next) rest[scope] = next;
    else delete rest[scope];
    setOwns(rest);
    window.api.noteTab.set(scope, next).catch((error) => {
      console.error('Failed to save the Raining of a key viewer', error);
      setOwns(owns);
    });
  };

  const change = (patch: Partial<NoteSettings>) => {
    const next = { ...values, ...patch };
    if (scope !== ALL) {
      saveOwn(ownRainingValues(next, shared));
      return;
    }
    setShared(next);
    window.api.settings.update({ noteSettings: next }).catch((error) => {
      console.error('Failed to save the Raining settings', error);
      setShared(shared);
    });
  };

  return { values, hasOwn: own !== null, change, forget: () => saveOwn(null) };
}

/**
 * A number that is kept when its field is left (Enter leaves it), not at
 * every digit typed: half-typed values would show in the running key viewer.
 */
function HeldNumber({
  value,
  onCommit,
  ...props
}: Omit<NumberInputProps, 'value' | 'onChange' | 'onBlur'> & {
  value: number;
  onCommit: (value: number) => void;
}) {
  const typed = useRef<number | null>(null);
  return (
    <NumberInput
      {...props}
      value={value}
      onChange={(next) => {
        typed.current = next;
      }}
      onBlur={() => {
        const next = typed.current;
        typed.current = null;
        if (next !== null && next !== value) onCommit(next);
      }}
    />
  );
}

/**
 * How Raining looks and behaves. Each value sits in its own row and is kept
 * as it is changed; the first row says which key viewer the rows are about.
 */
export function RainingGroups() {
  const { t } = useTranslation();
  const viewers = useKeyStore((state) => state.tabs);
  const owns = useSettingsStore((state) => state.tabNoteOverrides);
  const [chosen, setChosen] = useState(ALL);
  // A key viewer that was deleted meanwhile falls back to all of them.
  const scope = viewers.some((viewer) => viewer.id === chosen) ? chosen : ALL;
  const { values, hasOwn, change, forget } = useRaining(scope);

  // The time Raining takes to travel its length: showing the key press that
  // much later makes it meet the Raining.
  const travelMs =
    Math.round((values.trackHeight / values.speed) * 1000) +
    (values.delayedNoteEnabled ? values.shortNoteThresholdMs : 0);
  const recommendedDelay = Math.min(
    LIMITS.keyDisplayDelayMs.max,
    Math.max(LIMITS.keyDisplayDelayMs.min, travelMs),
  );

  const number = (
    key:
      | 'speed'
      | 'trackHeight'
      | 'shortNoteMinLengthPx'
      | 'shortNoteThresholdMs'
      | 'keyDisplayDelayMs',
    label: string,
    suffix: string,
    disabled = false,
  ) => (
    <HeldNumber
      value={values[key]}
      onCommit={(value) => change({ [key]: value })}
      min={LIMITS[key].min}
      max={LIMITS[key].max}
      suffix={suffix}
      width="84px"
      ariaLabel={label}
      disabled={disabled}
    />
  );

  // Raining that flows downward has its own two fade lengths.
  const fadeKeys = values.reverse
    ? (['reverseFadeTopPx', 'reverseFadeBottomPx'] as const)
    : (['fadeTopPx', 'fadeBottomPx'] as const);

  return (
    <>
      <section className="en-settings-group" data-settings-group="raining">
        <SettingRow
          label={t('raining.scope')}
          description={t('raining.scopeHelp')}
        >
          <Button
            // Kept in place while there is nothing to forget, so the list
            // beside it never moves.
            style={hasOwn ? undefined : { visibility: 'hidden' }}
            onClick={forget}
          >
            {t('raining.sameAsAll')}
          </Button>
          <Dropdown
            options={[
              { value: ALL, label: t('raining.scopeAll') },
              ...viewers.map((viewer) => {
                const name = `${t(`viewers.${viewer.viewerKind}`)} · ${viewer.name}`;
                return {
                  value: viewer.id,
                  label: owns[viewer.id]
                    ? t('raining.scopeOwn', { name })
                    : name,
                };
              }),
            ]}
            value={scope}
            onChange={setChosen}
            align="right"
            widthClass="max-w-[220px]"
            ariaLabel={t('raining.scope')}
          />
        </SettingRow>
        <SettingRow
          label={t('raining.speed')}
          description={t('raining.speedHelp')}
        >
          {number('speed', t('raining.speed'), 'px/s')}
        </SettingRow>
        <SettingRow
          label={t('raining.length')}
          description={t('raining.lengthHelp')}
        >
          {number('trackHeight', t('raining.length'), 'px')}
        </SettingRow>
        <SettingToggleRow
          label={t('raining.downward')}
          description={t('raining.downwardHelp')}
          checked={values.reverse}
          onToggle={() => change({ reverse: !values.reverse })}
        />
        <SettingRow
          label={t('raining.fade')}
          description={t('raining.fadeHelp')}
        >
          {fadeKeys.map((key, index) => {
            const label = t(
              index === 0 ? 'raining.fadeTop' : 'raining.fadeBottom',
            );
            return (
              <label key={key} className="en-settings-pair">
                <span>{label}</span>
                <HeldNumber
                  value={values[key]}
                  onCommit={(value) => change({ [key]: value })}
                  min={LIMITS[key].min}
                  max={LIMITS[key].max}
                  suffix="px"
                  width="72px"
                  ariaLabel={`${t('raining.fade')} ${label}`}
                />
              </label>
            );
          })}
        </SettingRow>
      </section>
      <section
        className="en-settings-group"
        data-settings-group="raining-short"
      >
        <SettingToggleRow
          label={t('raining.shortSame')}
          description={t('raining.shortSameHelp')}
          checked={values.delayedNoteEnabled}
          onToggle={() =>
            change({ delayedNoteEnabled: !values.delayedNoteEnabled })
          }
        />
        <SettingRow
          label={t('raining.shortLength')}
          description={t('raining.shortLengthHelp')}
        >
          {number(
            'shortNoteMinLengthPx',
            t('raining.shortLength'),
            'px',
            !values.delayedNoteEnabled,
          )}
        </SettingRow>
        <SettingRow
          label={t('raining.shortTime')}
          description={t('raining.shortTimeHelp')}
        >
          {number(
            'shortNoteThresholdMs',
            t('raining.shortTime'),
            'ms',
            !values.delayedNoteEnabled,
          )}
        </SettingRow>
        <SettingRow
          label={t('raining.keyDelay')}
          description={t('raining.keyDelayHelp')}
        >
          <Button
            disabled={values.keyDisplayDelayMs === recommendedDelay}
            onClick={() => change({ keyDisplayDelayMs: recommendedDelay })}
          >
            {t('raining.useRecommended', { value: recommendedDelay })}
          </Button>
          {number('keyDisplayDelayMs', t('raining.keyDelay'), 'ms')}
        </SettingRow>
      </section>
    </>
  );
}

/** How often Raining is drawn: a matter of the computer, not of the look. */
export function RainingFrameLimitRow() {
  const { t } = useTranslation();
  const { values, change } = useRaining(ALL);
  return (
    <SettingRow
      label={t('raining.frameLimit')}
      description={t('raining.frameLimitHelp')}
    >
      <HeldNumber
        value={values.frameLimit}
        onCommit={(value) => change({ frameLimit: value })}
        min={LIMITS.frameLimit.min}
        max={LIMITS.frameLimit.max}
        suffix="fps"
        width="84px"
        ariaLabel={t('raining.frameLimit')}
      />
    </SettingRow>
  );
}
