import { useTranslation } from '@contexts/useTranslation';
import KeyLimiterStatus from '@components/main/Tool/KeyLimiterStatus';
import ViewTabs from '@components/main/Viewers/ViewTabs';
import SettingIcon from '@assets/svgs/setting.svg';
import '@components/main/Viewers/viewers.css';
import './topbar.css';

interface TopBarProps {
  onOpenSettings: () => void;
  /** Opens settings on "Where to show", where the game connection lives. */
  onOpenOutputs: () => void;
}

/**
 * What the window's title bar holds while editing. Left: the tabs of the two
 * views, hands and feet; everything under the bar belongs to the open tab.
 * Right: the app, meaning the game connection and settings. Nothing here
 * depends on what is selected on the canvas.
 */
export default function TopBar({ onOpenSettings, onOpenOutputs }: TopBarProps) {
  const { t } = useTranslation();

  return (
    <>
      <ViewTabs />
      <div className="en-topbar-app">
        <KeyLimiterStatus onOpen={onOpenOutputs} />
        <button
          type="button"
          className="en-topbar-button"
          onClick={onOpenSettings}
        >
          <SettingIcon aria-hidden="true" />
          {t('tooltip.settings')}
        </button>
      </div>
    </>
  );
}
