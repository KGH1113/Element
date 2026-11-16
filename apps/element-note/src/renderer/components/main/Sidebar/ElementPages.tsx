import { useEffect, useRef, useState, type ReactNode } from 'react';
import Toggle from '@components/main/common/Toggle';
import { InspectorPageContext } from '@components/main/Grid/PropertiesPanel/inspectorPageContext';
import {
  usePartPreviewStore,
  type PreviewPart,
} from '@src/renderer/editor/runtime/partPreview';
import {
  Dimmed,
  RowText,
  SidebarFieldRow,
  SidebarGroup,
  SidebarPage,
  SidebarRow,
  type PageDirection,
} from './SidebarParts';
import {
  isPartOff,
  resolvePartPath,
  type ChooserPart,
  type ElementPart,
  type PagePart,
  type PartToggle,
} from './elementParts';

interface ElementPagesProps {
  direction: PageDirection;
  /** Name of the element list; the back button returns to it. */
  listLabel: string;
  onBackToList: () => void;
  /**
   * What is selected. The canvas shows it, so the element's page does not
   * repeat it as a title: it names the way back from the pages below.
   */
  title: string;
  /** Names what is selected: a chooser left open closes when it changes. */
  identity?: string;
  onDelete?: () => void;
  /** The element's parts, in groups: one row each, space between groups. */
  parts: ElementPart[][];
  /** Ids of the open pages, from the element's page down; empty on it. */
  path: readonly string[];
  /** Opens the page at `path`; `back` says it is returned to. */
  onNavigate: (path: string[], back: boolean) => void;
  /** Said under the rows of the element's page. */
  hint?: string;
  /** The settings of the open page. */
  children: ReactNode;
  t: (key: string) => string;
}

/**
 * A part shown working on the canvas while the pointer rests on its row. The
 * pointer has to stay a moment, so running it down the rows sets nothing off.
 */
const HOVER_DELAY_MS = 120;

function useHoverPreview(page: string) {
  const end = useRef<(() => void) | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const stop = () => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    end.current?.();
    end.current = null;
  };
  // A row that is pressed is left without the pointer leaving it.
  useEffect(() => stop, [page]);
  return (part: PreviewPart | undefined) =>
    part &&
    ((inside: boolean) => {
      stop();
      if (!inside) return;
      timer.current = setTimeout(() => {
        end.current = usePartPreviewStore.getState().show(part);
      }, HOVER_DELAY_MS);
    });
}

function ChooserRow({
  part,
  disabled,
}: {
  part: ChooserPart;
  disabled: boolean;
}) {
  const trigger = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState(false);
  return (
    <>
      <SidebarRow
        icon={part.icon}
        label={part.label}
        value={part.value}
        disabled={disabled}
        chooser={{ open, triggerRef: trigger }}
        onClick={() => setOpen((shown) => !shown)}
      />
      {open && part.chooser({ trigger, close: () => setOpen(false) })}
    </>
  );
}

/**
 * The element level of the sidebar. The element's page lists its parts, one
 * row each, every row led by its picture: a part with one value is edited in
 * its row, a part with a set to choose from opens a chooser beside its row,
 * an action is run from its row, and a part with many settings opens its
 * page.
 *
 * The element's page has no title and no name to edit: what is selected is
 * seen on the canvas, and names belong to the element list.
 */
export default function ElementPages({
  direction,
  listLabel,
  onBackToList,
  title,
  identity = '',
  onDelete,
  parts,
  path,
  onNavigate,
  hint,
  children,
  t,
}: ElementPagesProps) {
  const trail = resolvePartPath(parts, path);
  const ids = trail.map((part) => part.id);
  const page = trail[trail.length - 1] ?? null;
  const hover = useHoverPreview(ids.join('/'));
  // An open page keeps its part working on the canvas while it is open.
  const pagePreview = page?.preview;
  useEffect(
    () =>
      pagePreview
        ? usePartPreviewStore.getState().show(pagePreview)
        : undefined,
    [pagePreview, identity],
  );

  const stateText = (toggle: PartToggle) =>
    toggle.mixed
      ? t('sidebar.stateMixed')
      : toggle.checked
        ? (toggle.onLabel ?? t('sidebar.stateOn'))
        : t('sidebar.stateOff');

  const rows = (groups: ElementPart[][], base: string[], disabled: boolean) =>
    groups.map((group, index) => {
      const listed = group.filter(
        (part) =>
          part.kind !== 'page' || (part.listed !== false && !part.inPlace),
      );
      if (listed.length === 0) return null;
      return (
        <SidebarGroup key={index}>
          {listed.map((part) => {
            const key = `${identity}:${part.id}`;
            const off = disabled || !!part.disabled;
            if (part.kind === 'inline') {
              return (
                <SidebarFieldRow
                  key={key}
                  icon={part.icon}
                  label={part.label}
                  disabled={off}
                >
                  {part.control}
                </SidebarFieldRow>
              );
            }
            if (part.kind === 'chooser') {
              return <ChooserRow key={key} part={part} disabled={off} />;
            }
            if (part.kind === 'action') {
              return (
                <SidebarRow
                  key={key}
                  icon={part.icon}
                  label={part.label}
                  value={part.value}
                  disabled={off}
                  chevron={false}
                  onClick={part.run}
                />
              );
            }
            return (
              <SidebarRow
                key={key}
                icon={part.icon}
                label={part.label}
                value={
                  part.toggle ? (
                    <>
                      {part.value}
                      <RowText on={!isPartOff(part.toggle)}>
                        {stateText(part.toggle)}
                      </RowText>
                    </>
                  ) : (
                    part.value
                  )
                }
                disabled={off}
                onHover={hover(part.preview)}
                onClick={() => onNavigate([...base, part.id], false)}
              />
            );
          })}
        </SidebarGroup>
      );
    });

  if (page) {
    const parent = trail[trail.length - 2];
    // Under a part that is switched off, everything stays in place, greyed.
    const parentOff = trail.slice(0, -1).some((part) => isPartOff(part.toggle));
    return (
      <SidebarPage
        pageKey={`part:${ids.join('/')}`}
        direction={direction}
        back={{
          label: parent ? parent.label : title,
          onBack: () => onNavigate(ids.slice(0, -1), true),
        }}
        title={page.label}
        trailing={
          page.toggle && (
            <>
              <span className="en-page-state">
                {page.toggle.mixed
                  ? t('sidebar.stateMixed')
                  : page.toggle.checked
                    ? t('sidebar.stateOn')
                    : t('sidebar.stateOff')}
              </span>
              <Toggle
                checked={page.toggle.checked && !page.toggle.mixed}
                disabled={parentOff}
                ariaLabel={page.label}
                onChange={page.toggle.onChange}
              />
            </>
          )
        }
      >
        {page.parts ? (
          <>
            {rows(page.parts, ids, parentOff || isPartOff(page.toggle))}
            {page.hint && <p className="en-hint">{page.hint}</p>}
          </>
        ) : (
          <InspectorPageContext.Provider
            value={{ activePage: page.id, groups: page.groups }}
          >
            <Dimmed off={parentOff}>
              <div className="en-part-content">{children}</div>
            </Dimmed>
          </InspectorPageContext.Provider>
        )}
      </SidebarPage>
    );
  }

  const inPlace = parts
    .flat()
    .find((part): part is PagePart => part.kind === 'page' && !!part.inPlace);

  return (
    <SidebarPage
      pageKey="element"
      direction={direction}
      back={{ label: listLabel, onBack: onBackToList }}
      label={title}
      actions={
        onDelete && (
          <button
            type="button"
            className="en-text-button"
            data-tone="danger"
            onClick={onDelete}
          >
            {t('tooltip.delete')}
          </button>
        )
      }
    >
      {rows(parts, [], false)}
      {inPlace && (
        <InspectorPageContext.Provider
          value={{ activePage: inPlace.id, groups: inPlace.groups }}
        >
          <div className="en-part-content">{children}</div>
        </InspectorPageContext.Provider>
      )}
      {hint && <p className="en-hint">{hint}</p>}
    </SidebarPage>
  );
}
