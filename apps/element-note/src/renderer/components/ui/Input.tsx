import type { ComponentProps } from 'react';

// shadcn input behavior and focus treatment, adapted to Element's token names.
export function Input({
  className = '',
  type = 'text',
  ...props
}: ComponentProps<'input'>) {
  return (
    <input
      data-slot="input"
      type={type}
      className={`h-8 w-full min-w-0 rounded-md border border-ui-line-subtle bg-ui-inset px-2 text-xs text-ui-fg-primary outline-none transition-colors focus-visible:border-ui-accent-text focus-visible:ring-2 focus-visible:ring-ui-accent-soft disabled:pointer-events-none disabled:opacity-50 aria-invalid:border-red-400 ${className}`}
      {...props}
    />
  );
}
