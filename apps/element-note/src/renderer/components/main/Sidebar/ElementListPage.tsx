import type { ReactNode } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { SidebarPage, type PageDirection } from './SidebarParts';
import { SHELF_ADD_ICONS } from './sidebarIcons';

export type AddItemType = 'key' | 'stat' | 'graph' | 'knob' | 'sprite';

const SHELF: { type: AddItemType; label: string }[] = [
  { type: 'key', label: 'toolbar.addKey' },
  { type: 'stat', label: 'toolbar.addStat' },
  { type: 'graph', label: 'toolbar.addGraph' },
  { type: 'knob', label: 'toolbar.addKnob' },
  { type: 'sprite', label: 'sidebar.addSprite' },
];

export interface ElementListPageProps {
  direction: PageDirection;
  /** The elements of the open tab. */
  list: ReactNode;
  /** How many are picked in the list without being opened yet. */
  selectionCount: number;
  onOpenSelection: () => void;
  onAddItem: (type: AddItemType) => void;
}

/**
 * The first page of the sidebar: the elements of the open tab, and the kinds
 * that can be added. The sidebar is only ever about elements; pressing one
 * opens its page.
 */
export default function ElementListPage({
  direction,
  list,
  selectionCount,
  onOpenSelection,
  onAddItem,
}: ElementListPageProps) {
  const { t } = useTranslation();

  return (
    <SidebarPage
      pageKey="elements"
      direction={direction}
      title={t('sidebar.elements')}
      trailing={
        // Always there, so picking several never moves the title.
        <button
          type="button"
          className="en-text-button"
          style={{ visibility: selectionCount > 0 ? 'visible' : 'hidden' }}
          onClick={onOpenSelection}
        >
          {t('sidebar.openSelectionMany', { count: selectionCount })}
        </button>
      }
      fill
    >
      <section className="en-add" aria-label={t('sidebar.addElement')}>
        <h3 className="en-group-title">{t('sidebar.addElement')}</h3>
        <div className="en-shelf">
          {SHELF.map((item) => (
            <button
              key={item.type}
              type="button"
              className="en-shelf-item"
              aria-label={t('sidebar.addOne', { name: t(item.label) })}
              onClick={() => onAddItem(item.type)}
            >
              {SHELF_ADD_ICONS[item.type]}
              <span>{t(item.label)}</span>
            </button>
          ))}
        </div>
      </section>
      <div className="en-element-list">{list}</div>
    </SidebarPage>
  );
}
