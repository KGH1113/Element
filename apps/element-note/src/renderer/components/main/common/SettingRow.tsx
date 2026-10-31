import React, { type HTMLAttributes, type ReactNode } from 'react';
import Toggle from './Toggle';

type SettingCardProps = HTMLAttributes<HTMLDivElement> & {
  children: ReactNode;
  title?: string;
};

export const SettingCard = ({
  children,
  title,
  className = '',
  ...props
}: SettingCardProps): React.ReactElement => (
  <section className={`dmn-setting-card ${className}`} {...props}>
    {title && <h2 className="dmn-setting-card__title">{title}</h2>}
    {children}
  </section>
);

type SettingRowProps = HTMLAttributes<HTMLDivElement> & {
  label: ReactNode;
  description?: ReactNode;
  children: ReactNode;
};

export const SettingRow = ({
  label,
  description,
  children,
  className = '',
  ...props
}: SettingRowProps): React.ReactElement => (
  <div className={`dmn-setting-row ${className}`} {...props}>
    <div className="dmn-setting-row__copy">
      <div className="dmn-setting-row__label">{label}</div>
      {description && (
        <div className="dmn-setting-row__description">{description}</div>
      )}
    </div>
    <div className="dmn-setting-row__control">{children}</div>
  </div>
);

type SettingToggleRowProps = Omit<SettingRowProps, 'children'> & {
  checked: boolean;
  onToggle: () => void;
  disabled?: boolean;
  ariaLabel?: string;
};

export const SettingToggleRow = ({
  checked,
  onToggle,
  disabled = false,
  ariaLabel,
  ...props
}: SettingToggleRowProps): React.ReactElement => (
  <SettingRow
    {...props}
    className={`${props.className ?? ''} ${disabled ? 'is-disabled' : ''}`}
  >
    <Toggle
      checked={checked}
      disabled={disabled}
      ariaLabel={
        ariaLabel ?? (typeof props.label === 'string' ? props.label : undefined)
      }
      onChange={() => onToggle()}
    />
  </SettingRow>
);
