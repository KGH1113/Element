import { useLayoutEffect, useRef, type ReactNode, type Ref } from 'react';

/** Building blocks every sidebar page is made of: page, group, row. */

export type PageDirection = 'forward' | 'back' | 'none';

/**
 * How far each page was scrolled, kept while the app runs: going into a row
 * and coming back returns to the same place.
 */
const pageScrollMemory = new Map<string, number>();

interface SidebarPageProps {
  /** Changing this replays the entry motion. */
  pageKey: string;
  direction?: PageDirection;
  /** Where the back button leads; no button on the first page. */
  back?: { label: string; onBack: () => void };
  /** Replaces the back button on the first page. */
  lead?: ReactNode;
  actions?: ReactNode;
  /**
   * What the page is about. A page whose subject is in plain sight (the
   * element selected on the canvas) has none and is only named by `label`.
   */
  title?: ReactNode;
  /** Names a page without a title, for assistive technology. */
  label?: string;
  kind?: string;
  trailing?: ReactNode;
  /**
   * The body does not scroll: its children fill the page as a column, and
   * the one that can grow scrolls by itself.
   */
  fill?: boolean;
  children: ReactNode;
}

const BackChevron = () => (
  <svg
    width="14"
    height="14"
    viewBox="0 0 14 14"
    fill="none"
    aria-hidden="true"
  >
    <path
      d="M8.5 3 4.5 7l4 4"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

export function SidebarPage({
  pageKey,
  direction = 'none',
  back,
  lead,
  actions,
  title,
  label,
  kind,
  trailing,
  fill = false,
  children,
}: SidebarPageProps) {
  const pageRef = useRef<HTMLElement>(null);
  const bodyRef = useRef<HTMLDivElement>(null);
  // The head has no line of its own. One shows only while rows are scrolled
  // under it; it is set on the element, so scrolling renders nothing.
  const markScrolled = (top: number) =>
    pageRef.current?.toggleAttribute('data-scrolled', top > 0);
  useLayoutEffect(() => {
    if (bodyRef.current) {
      bodyRef.current.scrollTop = pageScrollMemory.get(pageKey) ?? 0;
      markScrolled(bodyRef.current.scrollTop);
    }
  }, [pageKey]);

  return (
    <section
      key={pageKey}
      ref={pageRef}
      className="en-page"
      data-dir={direction}
      aria-label={title === undefined ? label : undefined}
    >
      <header className="en-page-head">
        {(back || lead || actions) && (
          <div className="en-page-nav">
            {back ? (
              <button
                type="button"
                className="en-page-back"
                onClick={back.onBack}
              >
                <BackChevron />
                <span>{back.label}</span>
              </button>
            ) : (
              lead
            )}
            {actions && <div className="en-page-actions">{actions}</div>}
          </div>
        )}
        {title !== undefined && (
          <div className="en-page-title-row">
            {typeof title === 'string' ? (
              <h2 className="en-page-title" title={title}>
                {title}
              </h2>
            ) : (
              title
            )}
            {kind && <span className="en-page-kind">{kind}</span>}
            {trailing && <div className="en-page-trailing">{trailing}</div>}
          </div>
        )}
      </header>
      <div
        ref={bodyRef}
        className="en-page-body"
        data-fill={fill || undefined}
        onScroll={(event) => {
          pageScrollMemory.set(pageKey, event.currentTarget.scrollTop);
          markScrolled(event.currentTarget.scrollTop);
        }}
      >
        {children}
      </div>
    </section>
  );
}

interface SidebarGroupProps {
  title?: string;
  /** Sits at the right end of the group title. */
  action?: ReactNode;
  children: ReactNode;
}

/**
 * Rows that belong together. A group is a flat list: hairlines between its
 * rows, space between it and the next group, and no surface of its own.
 */
export function SidebarGroup({ title, action, children }: SidebarGroupProps) {
  return (
    <section className="en-group">
      {(title || action) && (
        <div className="en-group-head">
          <h3 className="en-group-title">{title}</h3>
          {action}
        </div>
      )}
      {children}
    </section>
  );
}

const PageChevron = () => (
  <svg
    className="en-row-chevron"
    width="12"
    height="12"
    viewBox="0 0 12 12"
    fill="none"
    aria-hidden="true"
  >
    <path
      d="m4.5 2.5 3.5 3.5-3.5 3.5"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

const ChooserChevron = () => (
  <svg
    className="en-row-chevron"
    width="12"
    height="12"
    viewBox="0 0 12 12"
    fill="none"
    aria-hidden="true"
  >
    <path
      d="M3.5 4.5 6 2l2.5 2.5M3.5 7.5 6 10l2.5-2.5"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

interface SidebarRowProps {
  icon?: ReactNode;
  label: string;
  /** Current value, shown at the right. */
  value?: ReactNode;
  disabled?: boolean;
  tone?: 'danger';
  /** Rows that run an action instead of opening a page have no chevron. */
  chevron?: boolean;
  /**
   * The row opens a chooser beside it instead of a page: it says so, and
   * says whether its chooser is the one that is open.
   */
  chooser?: { open: boolean; triggerRef: Ref<HTMLButtonElement> };
  /** The pointer came onto the row (true) or left it (false). */
  onHover?: (inside: boolean) => void;
  onClick: () => void;
}

/** A row that is pressed: it opens a page, opens a chooser or runs an action. */
export function SidebarRow({
  icon,
  label,
  value,
  disabled = false,
  tone,
  chevron = true,
  chooser,
  onHover,
  onClick,
}: SidebarRowProps) {
  return (
    <div
      className="en-row"
      data-disabled={disabled || undefined}
      data-open={chooser?.open || undefined}
      data-tone={tone}
      onPointerEnter={onHover && (() => onHover(true))}
      onPointerLeave={onHover && (() => onHover(false))}
    >
      <button
        ref={chooser?.triggerRef}
        type="button"
        className="en-row-hit"
        aria-label={label}
        aria-haspopup={chooser ? 'dialog' : undefined}
        aria-expanded={chooser ? chooser.open : undefined}
        disabled={disabled}
        onClick={onClick}
      />
      {icon && <span className="en-row-icon">{icon}</span>}
      <span className="en-row-label">{label}</span>
      <span className="en-row-value">{value}</span>
      {chooser ? <ChooserChevron /> : chevron && <PageChevron />}
    </div>
  );
}

interface SidebarFieldRowProps {
  icon?: ReactNode;
  label: string;
  disabled?: boolean;
  /** The control that edits the value, in the row itself. */
  children: ReactNode;
}

/**
 * A row that holds its control. It opens nothing: a single value, or a few
 * numbers, is edited right where it is listed.
 */
export function SidebarFieldRow({
  icon,
  label,
  disabled = false,
  children,
}: SidebarFieldRowProps) {
  return (
    <div
      className="en-row"
      data-kind="field"
      data-disabled={disabled || undefined}
      role="group"
      aria-label={label}
      inert={disabled}
    >
      {icon && <span className="en-row-icon">{icon}</span>}
      <span className="en-row-label">{label}</span>
      <span className="en-row-control">{children}</span>
    </div>
  );
}

/** A value in words. `on` is the word of a part that is switched on. */
export function RowText({
  on = false,
  children,
}: {
  on?: boolean;
  children: ReactNode;
}) {
  return (
    <span className="en-row-value-text" data-on={on || undefined}>
      {children}
    </span>
  );
}

/** A small picture of the image a row is about. */
export function RowImage({ src }: { src: string }) {
  return <img className="en-row-image" src={src} alt="" draggable={false} />;
}

export function Swatch({ color }: { color: string }) {
  return <span className="en-swatch" style={{ background: color }} />;
}

/** Keeps its children in place and greys them out while `off`. */
export function Dimmed({
  off,
  children,
}: {
  off: boolean;
  children: ReactNode;
}) {
  return (
    <div className="en-dimmed" data-off={off || undefined} inert={off}>
      {children}
    </div>
  );
}
