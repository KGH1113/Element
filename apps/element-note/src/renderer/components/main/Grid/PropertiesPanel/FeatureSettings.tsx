import type { ReactNode } from 'react';
export { default as SettingsGroup } from './PropertySection';
import Toggle from '@components/main/common/Toggle';
import { Dimmed } from '@components/main/Sidebar/SidebarParts';

interface FeatureSettingsProps {
  /** Whether the element shows this part (count or Raining). */
  enabled: boolean;
  /** Some of the selected elements show it and some do not. */
  mixed?: boolean;
  children: ReactNode;
}

/**
 * Settings of a part each element can switch off. The switch itself is on the
 * sidebar's row and page head; here the settings stay in place and are greyed
 * out while the part is off, so nothing moves when it is switched.
 */
export function FeatureSettings({
  enabled,
  mixed = false,
  children,
}: FeatureSettingsProps) {
  return <Dimmed off={!enabled && !mixed}>{children}</Dimmed>;
}

interface FeatureSwitchProps {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}

/** The part's switch, for dialogs that have no sidebar page head to hold it. */
export function FeatureSwitch({
  label,
  checked,
  onChange,
}: FeatureSwitchProps) {
  return (
    <div className="flex min-h-8 items-center justify-between gap-3">
      <span className="text-xs font-medium text-ui-fg-primary">{label}</span>
      <Toggle checked={checked} ariaLabel={label} onChange={onChange} />
    </div>
  );
}
