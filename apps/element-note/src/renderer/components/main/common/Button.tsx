import React from 'react';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'icon';

export interface ButtonProps extends Omit<
  React.ButtonHTMLAttributes<HTMLButtonElement>,
  'type'
> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  block?: boolean;
  loading?: boolean;
  iconOnly?: boolean;
  type?: 'button' | 'submit' | 'reset';
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      variant = 'secondary',
      size = 'md',
      block = false,
      loading = false,
      iconOnly = false,
      disabled,
      className = '',
      type = 'button',
      children,
      ...props
    },
    ref,
  ) => (
    <button
      {...props}
      ref={ref}
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      data-variant={variant}
      data-size={size}
      data-icon-only={iconOnly || undefined}
      className={`dmn-button ${block ? 'dmn-button--block' : ''} ${className}`}
    >
      {loading && <span className="dmn-button__spinner" aria-hidden="true" />}
      <span className="dmn-button__content">{children}</span>
    </button>
  ),
);

Button.displayName = 'Button';

export default Button;
