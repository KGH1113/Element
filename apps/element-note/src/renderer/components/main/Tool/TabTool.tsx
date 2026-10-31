import { useTranslation } from '@contexts/useTranslation';
import { useRef, useState } from 'react';
import { useKeyStore } from '@stores/data/useKeyStore';
import type { KeyViewerKind } from '@src/types/key/keys';
import FloatingPopup from '../Modal/FloatingPopup';
import FloatingTooltip from '../Modal/FloatingTooltip';
import { TooltipGroup } from '../Modal/TooltipGroup';
import TabList from '../Modal/content/settings/TabList';

const VIEWERS: Array<{
  kind: KeyViewerKind;
  number: string;
}> = [
  { kind: 'hand', number: '1' },
  { kind: 'foot', number: '2' },
];

const TabTool = () => {
  const { t } = useTranslation();
  const isBootstrapped = useKeyStore((state) => state.isBootstrapped);
  const selectedKeyType = useKeyStore((state) => state.selectedKeyType);
  const tabs = useKeyStore((state) => state.tabs);
  const activeViewer = tabs.find(
    (tab) => tab.id === selectedKeyType,
  )?.viewerKind;
  const [openViewer, setOpenViewer] = useState<KeyViewerKind | null>(null);
  const handButtonRef = useRef<HTMLButtonElement>(null);
  const footButtonRef = useRef<HTMLButtonElement>(null);
  const refs = { hand: handButtonRef, foot: footButtonRef };

  return (
    <TooltipGroup>
      <div className="flex gap-[8px]">
        {VIEWERS.map(({ kind, number }) => {
          const isOpen = openViewer === kind;
          const isActive = activeViewer === kind;
          const label = t(`tabs.${kind}Viewer`);
          return (
            <div key={kind} className="relative">
              <FloatingTooltip content={label} disabled={isOpen}>
                <button
                  ref={refs[kind]}
                  type="button"
                  aria-label={label}
                  aria-haspopup="dialog"
                  aria-expanded={isOpen}
                  aria-pressed={isActive}
                  data-open={isOpen ? 'true' : 'false'}
                  data-selected={isActive ? 'true' : 'false'}
                  disabled={!isBootstrapped}
                  className="viewer-tab-trigger dmn-tool-group relative flex h-[40px] w-[40px] items-center justify-center p-[5px] text-ui-fg-secondary disabled:cursor-not-allowed disabled:opacity-50"
                  onClick={() =>
                    setOpenViewer((current) => (current === kind ? null : kind))
                  }
                >
                  <span
                    className={`viewer-tab-trigger-surface dmn-tool-button flex h-[30px] w-[30px] items-center justify-center ${
                      isOpen
                        ? 'bg-button-active'
                        : 'hover:bg-button-hover active:bg-button-active'
                    }`}
                  >
                    <span
                      className="text-[18px] font-semibold leading-none tracking-[-0.02em]"
                      aria-hidden="true"
                    >
                      {number}
                    </span>
                  </span>
                  {isActive && (
                    <span
                      className="viewer-tab-selected-dot absolute bottom-[1px] left-1/2 h-[4px] w-[4px] -translate-x-1/2 rounded-full bg-ui-accent"
                      aria-hidden="true"
                    />
                  )}
                </button>
              </FloatingTooltip>
              <FloatingPopup
                open={isOpen && isBootstrapped}
                referenceRef={refs[kind]}
                placement="top"
                offset={10}
                portal
                ariaLabel={label}
                onClose={() => setOpenViewer(null)}
                className="viewer-tab-popup z-[100] origin-bottom"
              >
                <TabList viewerKind={kind} />
              </FloatingPopup>
            </div>
          );
        })}
      </div>
    </TooltipGroup>
  );
};

export default TabTool;
