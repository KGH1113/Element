import React from 'react';
import Close from '@assets/svgs/close.svg';
import Minimize from '@assets/svgs/minimize.svg';
import Logo from '@assets/svgs/logo.svg';
import { isMac } from '@utils/core/platform';

const TitleBar = (): React.ReactElement => {
  const isMacOS: boolean = isMac();
  const handleMinimize = (): void => {
    window.api.window.minimize();
  };

  const handleClose = (): void => {
    window.api.window.close();
  };

  return (
    <div
      data-tauri-drag-region
      className="dmn-titlebar relative flex h-[30px] min-h-[30px] w-full items-center justify-center [app-region:drag]"
      style={{ WebkitAppRegion: 'drag' } as React.CSSProperties}
    >
      <div className="pointer-events-none flex items-center gap-[6px] text-ui-fg-muted">
        <Logo className="h-[12px] w-[12px] shrink-0" />
        <div className="select-none text-[12px] font-semibold leading-[12px] tracking-[0.06em]">
          ElementNote
        </div>
      </div>
      {!isMacOS && (
        <div
          data-tauri-drag-region="false"
          className="absolute right-0 flex h-full [app-region:no-drag]"
          style={{ WebkitAppRegion: 'no-drag' } as React.CSSProperties}
        >
          <button
            type="button"
            aria-label="Minimize"
            onClick={handleMinimize}
            className="dmn-window-control flex h-full w-[38px] items-center justify-center"
          >
            <Minimize className="scale-[0.8] pointer-events-none" />
          </button>
          <button
            type="button"
            aria-label="Close"
            onClick={handleClose}
            className="dmn-window-control dmn-window-control--close flex h-full w-[38px] items-center justify-center"
          >
            <Close className="scale-[0.7] pointer-events-none" />
          </button>
        </div>
      )}
    </div>
  );
};

export default TitleBar;
