import React, { useState } from 'react';

export interface ToggleProps extends Omit<
  React.ButtonHTMLAttributes<HTMLButtonElement>,
  'onChange' | 'role'
> {
  checked: boolean;
  onChange: (checked: boolean) => void;
  ariaLabel?: string;
}

const Toggle = React.forwardRef<HTMLButtonElement, ToggleProps>(function Toggle(
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
) {
  // The knob stretches on its way only after it has been pressed, so a page
  // full of switches does not twitch when it opens.
  const [moved, setMoved] = useState(false);

  return (
    <button
      {...props}
      ref={ref}
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={ariaLabel}
      disabled={disabled}
      data-state={checked ? 'checked' : 'unchecked'}
      data-moved={moved || undefined}
      className={`dmn-toggle-track ${className}`}
      onClick={(event) => {
        onClick?.(event);
        if (event.defaultPrevented) return;
        setMoved(true);
        onChange(!checked);
      }}
    >
      <span className="dmn-toggle-thumb" aria-hidden="true">
        <span className="dmn-toggle-dot" />
      </span>
    </button>
  );
});

Toggle.displayName = 'Toggle';

export default Toggle;
