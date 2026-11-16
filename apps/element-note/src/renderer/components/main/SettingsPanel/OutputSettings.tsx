import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useKeyLimiterStatusStore } from '@stores/useKeyLimiterStatusStore';
import { useOverlayVisibility } from '@hooks/app/useOverlayVisibility';
import {
  SettingRow,
  SettingToggleRow,
} from '@components/main/common/SettingRow';
import Dropdown from '@components/main/common/Dropdown';
import Button from '@components/main/common/Button';
import type { ShowConfirm } from '@components/main/Modal/content/dialogs/confirm';
import { useKeyLimiterStatusPresentation } from '@components/main/Tool/keyLimiterStatusPresentation';
import { obsApi } from '@api/modules/obsApi';
import { DEFAULT_OBS_PORT, type ObsStatus } from '@src/types/obs';
import type { OverlayResizeAnchor } from '@src/types/settings/settings';
import { OutputButton, type OutputPlace } from './OutputButton';

/**
 * Where the key viewer is shown. There are three places (inside the game, on
 * the desktop, in an OBS broadcast) and one of them is in use at a time.
 *
 * The three are large buttons in a row, each with a small moving picture of
 * its place; pressing one chooses it. Under the buttons are the settings of
 * the chosen place only. The game's key limiter does not depend on where the
 * key viewer is shown, so it has a group of its own below.
 */

interface OutputSettingsProps {
  showAlert: (message: string) => void;
  showConfirm: ShowConfirm;
}

/** A row that waits for its group's switch: it stays in place, dimmed. */
const waiting = (on: boolean) => (on ? '' : 'en-settings-row--off');

const RESIZE_ANCHORS: { value: OverlayResizeAnchor; key: string }[] = [
  { value: 'top-left', key: 'topLeft' },
  { value: 'bottom-left', key: 'bottomLeft' },
  { value: 'top-right', key: 'topRight' },
  { value: 'bottom-right', key: 'bottomRight' },
  { value: 'center', key: 'center' },
];

type GameSetting = 'inGameViewersEnabled' | 'keyLimiterEnabled';

/** Inside ADOFAI: drawn by the Element mod, which also limits the keys. */
function useGameOutput(showAlert: OutputSettingsProps['showAlert']) {
  const { t, i18n } = useTranslation();
  const presentation = useKeyLimiterStatusPresentation();
  const inGameViewersEnabled = useSettingsStore(
    (state) => state.inGameViewersEnabled,
  );
  const keyLimiterEnabled = useSettingsStore(
    (state) => state.keyLimiterEnabled,
  );
  const acknowledgement = useKeyLimiterStatusStore(
    (state) => state.lastAcknowledgement,
  );
  const error = useKeyLimiterStatusStore((state) => state.error);
  const appliedAt = useMemo(() => {
    if (!acknowledgement) return null;
    return new Intl.DateTimeFormat(i18n.language, {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    }).format(acknowledgement.acknowledgedAt);
  }, [acknowledgement, i18n.language]);
  // The button already names the game; the state says only how the link is.
  const status =
    presentation.labelKey === 'keyLimiter.connected'
      ? t('outputs.connected')
      : t(presentation.labelKey);

  // Optimistic: the store follows the switch at once and rolls back if the
  // backend refuses the change.
  const change = (setting: GameSetting, current: boolean) => {
    const next = !current;
    useSettingsStore.getState().merge({ [setting]: next });
    window.api.settings.update({ [setting]: next }).catch((reason) => {
      console.error(`Failed to update ${setting}`, reason);
      useSettingsStore.getState().merge({ [setting]: current });
      showAlert(t('settingsWorkspace.changeFailed'));
    });
  };

  return {
    on: inGameViewersEnabled,
    set: (next: boolean) => {
      if (next !== inGameViewersEnabled) {
        change('inGameViewersEnabled', inGameViewersEnabled);
      }
    },
    status,
    tone: presentation.tone,
    keyLimiterEnabled,
    toggleKeyLimiter: () => change('keyLimiterEnabled', keyLimiterEnabled),
    acknowledgement,
    appliedAt,
    error,
  };
}

/** The game's key limiter: about the game, not about where to show. */
function KeyLimiterGroup({ game }: { game: ReturnType<typeof useGameOutput> }) {
  const { t } = useTranslation();
  const { acknowledgement } = game;
  const unsupportedKeys = acknowledgement?.unsupportedKeys ?? [];

  return (
    <section
      className="en-settings-group"
      data-output-group="limiter"
      aria-label={t('outputs.keyLimiter')}
    >
      <SettingToggleRow
        label={t('outputs.keyLimiter')}
        description={t('outputs.keyLimiterHelp')}
        checked={game.keyLimiterEnabled}
        onToggle={game.toggleKeyLimiter}
      />
      <SettingRow
        label={t('outputs.applied')}
        description={t('outputs.appliedHelp')}
      >
        <span className="en-settings-value">
          {acknowledgement
            ? t('outputs.appliedValue', {
                name: acknowledgement.profile.name,
                count: acknowledgement.supportedKeys.length,
                time: game.appliedAt ?? '',
              })
            : t('outputs.appliedWaiting')}
        </span>
      </SettingRow>
      {unsupportedKeys.length > 0 && (
        <p className="en-settings-status en-settings-warning" role="status">
          {t('outputs.unsupportedKeys', { keys: unsupportedKeys.join(', ') })}
        </p>
      )}
      {game.error && (
        <p className="en-settings-status en-settings-warning" role="status">
          {game.error}
        </p>
      )}
    </section>
  );
}

/** On the desktop: the app's own always-on-top windows. */
function DesktopOutputDetails({
  showAlert,
}: Pick<OutputSettingsProps, 'showAlert'>) {
  const { t } = useTranslation();
  const overlayLocked = useSettingsStore((state) => state.overlayLocked);
  const setOverlayLocked = useSettingsStore((state) => state.setOverlayLocked);
  const alwaysOnTop = useSettingsStore((state) => state.alwaysOnTop);
  const setAlwaysOnTop = useSettingsStore((state) => state.setAlwaysOnTop);
  const overlayResizeAnchor = useSettingsStore(
    (state) => state.overlayResizeAnchor,
  );
  const setOverlayResizeAnchor = useSettingsStore(
    (state) => state.setOverlayResizeAnchor,
  );

  const changeLock = async () => {
    const next = !overlayLocked;
    setOverlayLocked(next);
    try {
      await window.api.overlay.setLock(next);
    } catch (error) {
      console.error('Failed to toggle overlay lock', error);
      setOverlayLocked(!next);
      showAlert(t('settingsWorkspace.changeFailed'));
    }
  };

  const changeOnTop = async () => {
    const next = !alwaysOnTop;
    setAlwaysOnTop(next);
    try {
      await window.api.settings.update({ alwaysOnTop: next });
    } catch (error) {
      console.error('Failed to toggle always-on-top', error);
      setAlwaysOnTop(!next);
      showAlert(t('settingsWorkspace.changeFailed'));
    }
  };

  const changeAnchor = async (value: string) => {
    const previous = overlayResizeAnchor;
    setOverlayResizeAnchor(value as OverlayResizeAnchor);
    try {
      await window.api.overlay.setAnchor(value);
    } catch (error) {
      console.error('Failed to set overlay anchor', error);
      setOverlayResizeAnchor(previous);
      showAlert(t('settingsWorkspace.changeFailed'));
    }
  };

  return (
    <section
      className="en-settings-group"
      data-output="desktop"
      aria-label={t('outputs.desktop')}
    >
      <SettingToggleRow
        label={t('outputs.lock')}
        description={t('settingsWorkspace.lockHelp')}
        checked={overlayLocked}
        onToggle={() => void changeLock()}
      />
      <SettingToggleRow
        label={t('outputs.onTop')}
        description={t('settingsWorkspace.topHelp')}
        checked={alwaysOnTop}
        onToggle={() => void changeOnTop()}
      />
      <SettingRow
        label={t('outputs.anchor')}
        description={t('settingsWorkspace.anchorHelp')}
      >
        <Dropdown
          options={RESIZE_ANCHORS.map((anchor) => ({
            value: anchor.value,
            label: t(`settings.${anchor.key}`),
          }))}
          value={overlayResizeAnchor}
          onChange={(value: string) => void changeAnchor(value)}
          placeholder={t('settings.selectAnchor')}
          align="right"
        />
      </SettingRow>
    </section>
  );
}

/** In a broadcast: OBS loads the key viewer as a browser source. An extra. */
function useObsOutput({ showAlert, showConfirm }: OutputSettingsProps) {
  const { t } = useTranslation();
  const obsModeEnabled = useSettingsStore((state) => state.obsModeEnabled);
  const [status, setStatus] = useState<ObsStatus>({
    running: false,
    port: DEFAULT_OBS_PORT,
    clientCount: 0,
  });
  const switching = useRef(false);

  useEffect(() => {
    let mounted = true;
    const apply = (next: ObsStatus) => {
      if (mounted) setStatus(next);
    };
    obsApi
      .status()
      .then(apply)
      .catch(() => undefined);
    const unsubscribe = obsApi.onStatus(apply);
    return () => {
      mounted = false;
      unsubscribe();
    };
  }, []);

  // Sources coming and going raise no event, so the count is asked for, and
  // only while there is a count to show.
  useEffect(() => {
    if (!obsModeEnabled) return;
    let mounted = true;
    const interval = setInterval(() => {
      obsApi
        .status()
        .then((next) => {
          if (mounted) setStatus(next);
        })
        .catch(() => undefined);
    }, 5000);
    return () => {
      mounted = false;
      clearInterval(interval);
    };
  }, [obsModeEnabled]);

  // The address is only handed out while the switch is on.
  const offered = obsModeEnabled && status.running;

  const toggle = async () => {
    if (switching.current) return;
    switching.current = true;
    const next = !obsModeEnabled;
    useSettingsStore.getState().merge({ obsModeEnabled: next });
    try {
      setStatus(next ? await obsApi.start() : await obsApi.stop());
    } catch (error) {
      console.error('Failed to switch the OBS browser source', error);
      useSettingsStore.getState().merge({ obsModeEnabled: !next });
      showAlert(t('outputs.obsFailed'));
    } finally {
      switching.current = false;
    }
  };

  const copyAddress = async () => {
    const token = status.token ? `?token=${status.token}` : '';
    const address = `http://${status.localIp || 'localhost'}:${status.port}${token}`;
    try {
      await navigator.clipboard.writeText(address);
      showAlert(t('outputs.copied'));
    } catch {
      // Without clipboard access the address is shown to copy by hand.
      showAlert(address);
    }
  };

  const replaceAddress = () =>
    showConfirm(
      t('outputs.newAddressConfirm'),
      async () => {
        try {
          setStatus(await obsApi.regenerateToken());
        } catch (error) {
          console.error('Failed to make a new OBS address', error);
          showAlert(t('settingsWorkspace.changeFailed'));
        }
      },
      // The address in use stops working.
      { confirmText: t('outputs.newAddress'), danger: true },
    );

  return {
    on: obsModeEnabled,
    set: (next: boolean) => {
      if (next !== obsModeEnabled) void toggle();
    },
    offered,
    clientCount: status.clientCount,
    copyAddress: () => void copyAddress(),
    replaceAddress,
  };
}

function ObsOutputDetails({ obs }: { obs: ReturnType<typeof useObsOutput> }) {
  const { t } = useTranslation();
  return (
    <section
      className="en-settings-group"
      data-output="obs"
      aria-label={t('outputs.obs')}
    >
      <SettingRow
        className={waiting(obs.offered)}
        label={t('outputs.address')}
        description={t('outputs.addressHelp')}
      >
        <Button
          variant="primary"
          disabled={!obs.offered}
          onClick={obs.copyAddress}
        >
          {t('outputs.copy')}
        </Button>
        <Button
          variant="ghost"
          disabled={!obs.offered}
          onClick={obs.replaceAddress}
        >
          {t('outputs.newAddress')}
        </Button>
      </SettingRow>
    </section>
  );
}

export function OutputSettings(props: OutputSettingsProps) {
  const { t } = useTranslation();
  const game = useGameOutput(props.showAlert);
  const overlay = useOverlayVisibility();
  const obs = useObsOutput(props);

  // One place at a time. If older settings left several on, the first in
  // this order counts until the user chooses.
  const chosen: OutputPlace | null = game.on
    ? 'game'
    : overlay.visible
      ? 'desktop'
      : obs.on
        ? 'obs'
        : null;
  const choose = (place: OutputPlace) => {
    game.set(place === 'game');
    overlay.set(place === 'desktop');
    obs.set(place === 'obs');
  };
  const inUse = t('outputs.inUse');

  return (
    <>
      <div
        className="en-outputs"
        role="radiogroup"
        aria-label={t('outputs.switches')}
      >
        <OutputButton
          place="game"
          name={t('outputs.game')}
          description={t('outputs.gameHelp')}
          chosen={chosen === 'game'}
          state={chosen === 'game' ? game.status : ''}
          tone={game.tone}
          onChoose={choose}
        />
        <OutputButton
          place="desktop"
          name={t('outputs.desktop')}
          description={t('outputs.desktopHelp')}
          chosen={chosen === 'desktop'}
          state={chosen === 'desktop' ? inUse : ''}
          onChoose={choose}
        />
        <OutputButton
          place="obs"
          name={t('outputs.obs')}
          description={t('outputs.obsHelp')}
          chosen={chosen === 'obs'}
          state={
            chosen !== 'obs'
              ? ''
              : !obs.offered
                ? inUse
                : obs.clientCount > 0
                  ? t('outputs.obsConnected', { count: obs.clientCount })
                  : t('outputs.obsWaiting')
          }
          tone={obs.clientCount > 0 ? 'success' : undefined}
          onChoose={choose}
        />
      </div>
      {/* Only what belongs to the chosen place. Inside the game there is
          nothing to set: its button already says how the connection is. */}
      {chosen === 'desktop' && (
        <DesktopOutputDetails showAlert={props.showAlert} />
      )}
      {chosen === 'obs' && <ObsOutputDetails obs={obs} />}
      <KeyLimiterGroup game={game} />
    </>
  );
}
