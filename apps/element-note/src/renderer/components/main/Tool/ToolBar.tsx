import CanvasTool from './CanvasTool';
import SettingTool from './SettingTool';
import TabTool from './TabTool';
import Github from '@assets/svgs/github.svg';
import Bug from '@assets/svgs/code.svg';
import NoteIcon from '@assets/svgs/note.svg';
import { TooltipGroup } from '../Modal/TooltipGroup';
import { useTranslation } from '@contexts/useTranslation';
import FloatingTooltip from '../Modal/FloatingTooltip';
import { useSettingsStore } from '@stores/useSettingsStore';
import KeyLimiterStatus from './KeyLimiterStatus';

interface ToolBarProps {
  onAddItem: (type: 'key' | 'stat' | 'graph' | 'knob') => void;
  onTogglePalette: () => void;
  isPaletteOpen: boolean;
  onResetCurrentMode: () => void;
  onResetCounters?: () => void;
  activeTool: string;
  setActiveTool: (tool: string) => void;
  isSettingsOpen?: boolean;
  onOpenSettings?: () => void;
  onCloseSettings?: () => void;
  showAlert?: (message: string) => void;
  onOpenNoteSetting?: () => void;
  primaryButtonRef?: React.RefObject<HTMLButtonElement>;
}

const ToolBar = ({
  onAddItem,
  onTogglePalette,
  isPaletteOpen,
  onResetCurrentMode,
  onResetCounters,
  activeTool,
  setActiveTool,
  isSettingsOpen = false,
  onOpenSettings,
  onCloseSettings,
  showAlert,
  onOpenNoteSetting,
  primaryButtonRef,
}: ToolBarProps) => {
  const { t } = useTranslation();
  const handleClick = (link: string) => {
    window.api.app.openExternal(link);
  };

  return (
    <div className="dmn-toolbar grid h-[60px] min-h-[60px] w-full grid-cols-[auto_minmax(0,1fr)_auto] items-center px-[10px] py-[10px]">
      <div className="flex min-w-0 justify-self-start overflow-hidden">
        {isSettingsOpen ? (
          <TooltipGroup>
            <div className="flex gap-[8px]">
              <FloatingTooltip content={t('tooltip.github')}>
                <button
                  type="button"
                  aria-label={t('tooltip.github')}
                  onClick={() =>
                    handleClick('https://github.com/KGH1113/Element')
                  }
                  className="dmn-tool-group flex h-[40px] w-[40px] items-center justify-center p-[5px]"
                >
                  <div className="dmn-tool-button flex h-full w-full items-center justify-center">
                    <Github className="flex-shrink-0 mb-[3px]" />
                  </div>
                </button>
              </FloatingTooltip>
              <FloatingTooltip content={t('tooltip.issue')}>
                <button
                  type="button"
                  aria-label={t('tooltip.issue')}
                  onClick={() =>
                    handleClick('https://github.com/KGH1113/Element/issues')
                  }
                  className="dmn-tool-group flex h-[40px] w-[112px] items-center justify-center p-[5px]"
                >
                  <div className="dmn-tool-button flex h-full w-full items-center justify-center gap-[8px] px-[8px]">
                    <Bug className="flex-shrink-0" />
                    <p className="text-body truncate text-ui-fg-secondary">
                      {/* {t("tooltip.issue")} */}
                      Report
                    </p>
                  </div>
                </button>
              </FloatingTooltip>
            </div>
          </TooltipGroup>
        ) : (
          <TabTool />
        )}
      </div>
      <div className="key-limiter-status-slot flex min-w-0 items-center justify-center px-[12px]">
        <KeyLimiterStatus />
      </div>
      <div className="flex gap-[8px] justify-self-end">
        {!isSettingsOpen && (
          <CanvasTool
            onAddItem={onAddItem}
            onTogglePalette={onTogglePalette}
            isPaletteOpen={isPaletteOpen}
            onResetCurrentMode={onResetCurrentMode}
            onResetCounters={onResetCounters}
            activeTool={activeTool}
            setActiveTool={setActiveTool}
            primaryButtonRef={primaryButtonRef}
          />
        )}
        {!isSettingsOpen && (
          <TrackSettingButton onOpenNoteSetting={onOpenNoteSetting} t={t} />
        )}
        <SettingTool
          isSettingsOpen={isSettingsOpen}
          onOpenSettings={onOpenSettings}
          onCloseSettings={onCloseSettings}
          showAlert={showAlert}
          // onOpenNoteSetting={onOpenNoteSetting}
        />
      </div>
    </div>
  );
};

const TrackSettingButton = ({
  onOpenNoteSetting,
  t,
}: {
  onOpenNoteSetting?: () => void;
  t: (key: string) => string;
}) => {
  const { noteEffect } = useSettingsStore();

  if (!noteEffect) return null;

  return (
    <TooltipGroup>
      <div className="dmn-tool-group flex h-[40px] items-center p-[5px]">
        <FloatingTooltip content={t('tooltip.trackSettings') || '트랙 설정'}>
          <button
            type="button"
            aria-label={t('tooltip.trackSettings') || '트랙 설정'}
            onClick={onOpenNoteSetting}
            className="dmn-tool-button flex h-[30px] w-[30px] items-center justify-center"
          >
            <NoteIcon />
          </button>
        </FloatingTooltip>
      </div>
    </TooltipGroup>
  );
};

export default ToolBar;
