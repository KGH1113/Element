import React, { type ReactNode } from 'react';
import CloseIcon from '@assets/svgs/close.svg';

export type SettingsPanelKey = 'shortcuts' | 'plugins' | 'css';

interface SettingsSidePanelProps {
  title: string;
  badge?: ReactNode;
  children: ReactNode;
  onClose: () => void;
}

const SettingsSidePanel = ({
  title,
  badge,
  children,
  onClose,
}: SettingsSidePanelProps): React.ReactElement => (
  <section className="dmn-settings-side-panel" aria-label={title}>
    <header className="dmn-settings-side-panel__header">
      <div className="min-w-0">
        <div className="flex items-center gap-[8px]">
          <h2 className="text-title truncate text-ui-fg-primary">{title}</h2>
          {badge}
        </div>
        <p className="text-caption text-ui-fg-muted">ElementNote</p>
      </div>
      <button
        type="button"
        className="dmn-panel-icon-button"
        aria-label="Close settings panel"
        onClick={onClose}
      >
        <CloseIcon className="h-[10px] w-[10px]" />
      </button>
    </header>
    <div className="dmn-settings-side-panel__body">{children}</div>
  </section>
);

export default SettingsSidePanel;
