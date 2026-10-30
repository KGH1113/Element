import React from 'react';

export interface ToggleProps extends Omit<
  React.ButtonHTMLAttributes<HTMLButtonElement>,
  'onChange' | 'role'
> {
  checked: boolean;
  onChange: (checked: boolean) => void;
  ariaLabel?: string;
}

const Toggle = React.forwardRef<HTMLButtonElement, ToggleProps>(
  (
    {
      checked,
      onChange,
      disabled = false,
      ariaLabel,
      className = '',
      onClick,
      ...props
    },
    ref,
  ) => (
    <button
      {...props}
      ref={ref}
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={ariaLabel}
      disabled={disabled}
      data-state={checked ? 'checked' : 'unchecked'}
      className={`dmn-toggle-track ${className}`}
      onClick={(event) => {
        onClick?.(event);
        if (!event.defaultPrevented) onChange(!checked);
      }}
    >
      <span className="dmn-toggle-thumb" aria-hidden="true" />
    </button>
  ),
);

Toggle.displayName = 'Toggle';

export default Toggle;
