import type { ComponentProps } from 'react';

// shadcn card structure, using the existing Element color and radius tokens.
export function Card({ className = '', ...props }: ComponentProps<'section'>) {
  return (
    <section
      data-slot="card"
      className={`rounded-xl border border-ui-line-subtle bg-ui-surface p-4 ${className}`}
      {...props}
    />
  );
}

export function CardTitle({ className = '', ...props }: ComponentProps<'h3'>) {
  return (
    <h3
      data-slot="card-title"
      className={`mb-3 text-sm font-semibold text-ui-fg-primary ${className}`}
      {...props}
    />
  );
}
