import React, { type ReactNode } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import Close from '@assets/svgs/close.svg';
import Minimize from '@assets/svgs/minimize.svg';
import Logo from '@assets/svgs/logo.svg';
import { isMac } from '@utils/core/platform';
import './TopBar/topbar.css';

interface TitleBarProps {
  /**
   * What the window's own bar holds while editing. Without it the bar shows
   * the app name. The bar keeps one height either way, so the operating
   * system's window buttons always sit in its middle.
   */
  children?: ReactNode;
}

/**
 * The top line of the window. It is the operating system's title bar and the
 * editor's bar in one: the window can be dragged by any empty part of it.
 */
const TitleBar = ({ children }: TitleBarProps): React.ReactElement => {
  const { t } = useTranslation();
  const isMacOS: boolean = isMac();
  const handleMinimize = (): void => {
    window.api.window.minimize();
  };

  const handleClose = (): void => {
    window.api.window.close();
  };

  const handleMaximize = (): void => {
    window.api.window.toggleMaximize();
  };

  return (
    <div
      data-tauri-drag-region
      data-platform={isMacOS ? 'mac' : 'other'}
      className="dmn-titlebar en-titlebar"
    >
      {children ? (
        // Empty room between the two ends drags the window.
        <div data-tauri-drag-region className="en-titlebar-content">
          {children}
        </div>
      ) : (
        <div data-tauri-drag-region className="en-titlebar-name">
          <Logo className="h-[12px] w-[12px] shrink-0" />
          <span>ElementNote</span>
        </div>
      )}
      {!isMacOS && (
        <div data-tauri-drag-region="false" className="en-titlebar-controls">
          <button
            type="button"
            aria-label={t('windowControls.minimize')}
            title={t('windowControls.minimize')}
            onClick={handleMinimize}
            className="dmn-window-control flex h-full w-[42px] items-center justify-center"
          >
            <Minimize className="scale-[0.8] pointer-events-none" />
          </button>
          <button
            type="button"
            aria-label={t('windowControls.maximize')}
            title={t('windowControls.maximize')}
            onClick={handleMaximize}
            className="dmn-window-control flex h-full w-[42px] items-center justify-center"
          >
            <svg
              viewBox="0 0 16 16"
              width="12"
              height="12"
              fill="none"
              aria-hidden="true"
            >
              <rect
                x="2.5"
                y="2.5"
                width="11"
                height="11"
                rx="1"
                stroke="currentColor"
              />
            </svg>
          </button>
          <button
            type="button"
            aria-label={t('windowControls.close')}
            title={t('windowControls.close')}
            onClick={handleClose}
            className="dmn-window-control dmn-window-control--close flex h-full w-[42px] items-center justify-center"
          >
            <Close className="scale-[0.7] pointer-events-none" />
          </button>
        </div>
      )}
    </div>
  );
};

export default TitleBar;
