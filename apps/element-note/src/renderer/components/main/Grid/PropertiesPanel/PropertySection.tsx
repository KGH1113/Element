import type { ReactNode } from 'react';
import { useInspectorPages } from './inspectorPageContext';

interface PropertySectionProps {
  /** Names the group, so a sidebar part can show only some groups. */
  id: string;
  title: string;
  description?: string;
  /** Kept for callers; groups are always open. */
  defaultOpen?: boolean;
  children: ReactNode;
}

/**
 * One titled group of settings on a part page. Groups never fold, so the
 * settings below them never move.
 */
export default function PropertySection({
  id,
  title,
  description,
  children,
}: PropertySectionProps) {
  const groups = useInspectorPages()?.groups;
  if (groups && !groups.includes(id)) return null;
  return (
    <section className="en-section" aria-label={title}>
      <h3 className="en-section-title">{title}</h3>
      <div className="en-section-card">
        {description && <p className="en-section-description">{description}</p>}
        {children}
      </div>
    </section>
  );
}
