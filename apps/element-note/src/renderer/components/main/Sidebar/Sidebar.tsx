import { useRef, useState, type ReactNode } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { useChooserScope } from './chooserScopeContext';
import { SIDEBAR_WIDTH, useSidebarStore } from './sidebarStore';
import './sidebar.css';

interface SidebarProps {
  /** The element list, or the page of the element that is open. */
  children: ReactNode;
}

const KEY_STEP = 16;
// However wide the sidebar is dragged, the canvas keeps this much room.
const CANVAS_MIN_WIDTH = 320;

/**
 * The docked sidebar. It is only about the elements of the open tab: their
 * list, and the page of the one that is open. It takes its own column next to
 * the canvas, and its left edge can be dragged to change the width.
 */
export default function Sidebar({ children }: SidebarProps) {
  const { t } = useTranslation();
  const width = useSidebarStore((state) => state.width);
  const setWidth = useSidebarStore((state) => state.setWidth);
  const chooserScope = useChooserScope();
  const [dragging, setDragging] = useState(false);
  const dragRef = useRef<{
    pointerId: number;
    startX: number;
    startWidth: number;
  } | null>(null);

  return (
    <aside
      className="en-sidebar"
      style={{ width, maxWidth: `calc(100% - ${CANVAS_MIN_WIDTH}px)` }}
      aria-label={t('sidebar.title')}
    >
      <div
        className="en-sidebar-resizer"
        role="separator"
        aria-orientation="vertical"
        aria-label={t('sidebar.resize')}
        aria-valuemin={SIDEBAR_WIDTH.min}
        aria-valuemax={SIDEBAR_WIDTH.max}
        aria-valuenow={width}
        tabIndex={0}
        title={t('sidebar.resizeHint')}
        data-dragging={dragging || undefined}
        onPointerDown={(event) => {
          if (event.button !== 0) return;
          event.preventDefault();
          event.currentTarget.setPointerCapture(event.pointerId);
          dragRef.current = {
            pointerId: event.pointerId,
            startX: event.clientX,
            startWidth: width,
          };
          setDragging(true);
        }}
        onPointerMove={(event) => {
          const drag = dragRef.current;
          if (!drag || drag.pointerId !== event.pointerId) return;
          // The handle is on the left edge: dragging left makes it wider.
          setWidth(drag.startWidth + drag.startX - event.clientX);
        }}
        onPointerUp={(event) => {
          if (dragRef.current?.pointerId !== event.pointerId) return;
          dragRef.current = null;
          setDragging(false);
          setWidth(useSidebarStore.getState().width, true);
        }}
        onPointerCancel={() => {
          dragRef.current = null;
          setDragging(false);
        }}
        onDoubleClick={() => setWidth(SIDEBAR_WIDTH.default, true)}
        onKeyDown={(event) => {
          if (event.key === 'ArrowLeft') setWidth(width + KEY_STEP, true);
          else if (event.key === 'ArrowRight') setWidth(width - KEY_STEP, true);
          else if (event.key === 'Home') setWidth(SIDEBAR_WIDTH.default, true);
          else return;
          event.preventDefault();
        }}
      />
      <div className="en-sidebar-stack" ref={chooserScope?.setElement}>
        {children}
      </div>
    </aside>
  );
}
