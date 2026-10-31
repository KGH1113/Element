import React from 'react';

interface TabItem {
  id: string;
  label: string;
}

interface TabSwitchProps {
  tabs: TabItem[];
  activeTab: string;
  onTabChange: (tabId: string) => void;
  className?: string;
}

const TabSwitch = ({
  tabs,
  activeTab,
  onTabChange,
  className,
}: TabSwitchProps) => {
  const activeIndex = Math.max(
    0,
    tabs.findIndex((tab) => tab.id === activeTab),
  );

  return (
    <div className={`dmn-segmented ${className ?? ''}`}>
      {tabs.length > 0 && (
        <span
          className="dmn-segment-thumb"
          aria-hidden="true"
          style={{
            width: `calc((100% - 6px) / ${tabs.length})`,
            transform: `translate3d(${activeIndex * 100}%, 0, 0)`,
          }}
        />
      )}
      {tabs.map((tab) => {
        const selected = activeTab === tab.id;
        return (
          <button
            key={tab.id}
            type="button"
            aria-pressed={selected}
            onClick={() => onTabChange(tab.id)}
            className="dmn-segment-button"
          >
            {tab.label}
          </button>
        );
      })}
    </div>
  );
};

export default TabSwitch;
