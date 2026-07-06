'use client'

import {
  useRef,
  type CSSProperties,
  type MouseEvent,
  type ReactNode,
  type TouchEvent,
} from 'react'
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons'
import { type Header, flexRender } from '@tanstack/react-table'
import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import WaydTooltip from '@/src/components/common/wayd-tooltip'

/**
 * A header's content, wrapped in a {@link WaydTooltip} when the column
 * declares `meta.headerTooltip` — columns keep a plain-string `header` (which
 * CSV export also reads) instead of hand-rolling a Tooltip header renderer.
 */
export function GridHeaderContent<T>({ header }: { header: Header<T, unknown> }) {
  // eslint-disable-next-line react-compiler/react-compiler -- same mutable-header caveat as GridHeaderCell
  'use no memo'
  if (header.isPlaceholder) return null

  const content = flexRender(
    header.column.columnDef.header,
    header.getContext(),
  )
  const tooltip = header.column.columnDef.meta?.headerTooltip
  if (!tooltip) return content

  return (
    <WaydTooltip title={tooltip}>
      <span>{content}</span>
    </WaydTooltip>
  )
}

/**
 * Guards the header's click-to-sort against the click that ends a column
 * resize: the mouseup that finishes a drag-resize lands on the `<th>` and
 * would otherwise toggle the sort.
 */
export interface ResizeClickGuard {
  /** Call on resize-handle mousedown/touchstart. */
  beginResize: () => void
  /** Call from the sort click handler; returns true when the click ended a
   *  resize and must be swallowed instead of toggling the sort. */
  consumeResizeClick: () => boolean
}

/** Creates the {@link ResizeClickGuard} shared by all of a grid's header cells. */
export function useResizeClickGuard(): ResizeClickGuard {
  const isResizingRef = useRef(false)

  const beginResize = () => {
    isResizingRef.current = true
  }

  const consumeResizeClick = () => {
    if (isResizingRef.current) {
      isResizingRef.current = false
      return true
    }
    return false
  }

  return { beginResize, consumeResizeClick }
}

/**
 * CSS-module class names the owning grid supplies for the header cell. Both
 * grids define the same class set in their own module; passing them in keeps
 * each grid's visual styling untouched by the shared markup.
 */
export interface GridHeaderCellClasses {
  th: string
  thSortable: string
  thResizable: string
  thContent: string
  thText: string
  resizer: string
  resizerActive: string
  /** Applied to the `<th>` when the column is reorderable (grab cursor). */
  thDraggable?: string
  /** Applied to the `<th>` while this column is being dragged. */
  thDragging?: string
}

/**
 * Sortable wiring for a reorderable header cell, produced by
 * {@link useHeaderCellSortable} and threaded into {@link GridHeaderCell}. The
 * whole `<th>` is the drag handle: it carries `handleProps` (drag listeners),
 * `setNodeRef`, and `style` so it translates while dragging.
 */
export interface HeaderCellSortable {
  setNodeRef: (node: HTMLElement | null) => void
  style: CSSProperties
  isDragging: boolean
  /** Spread onto the `<th>` (drag listeners + a11y attributes). */
  handleProps: Record<string, unknown>
}

export interface GridHeaderCellProps<T> {
  header: Header<T, unknown>
  /** The grid's {@link useResizeClickGuard}, so the click that ends a column
   *  resize doesn't toggle the sort. */
  resizeGuard: ResizeClickGuard
  classes: GridHeaderCellClasses
  /** Optional filter affordance rendered after the sort icon (e.g. the
   *  wayd-grid filter popover trigger). */
  filterSlot?: ReactNode
  /** Optional column-menu trigger rendered last in the header content (the
   *  wayd-grid ⋮ dropdown). */
  menuSlot?: ReactNode
  /** Extra class for the `<th>` (e.g. the grid's pinned-column class). */
  thClassName?: string
  /** Extra inline style for the `<th>` (e.g. the sticky pinning inset). */
  thStyle?: CSSProperties
  /** Column-reorder sortable wiring from {@link useHeaderCellSortable}. When
   *  omitted the cell isn't draggable (grouped-header grids, the actions
   *  column, or a placeholder). */
  sortable?: HeaderCellSortable
}

/**
 * Per-header sortable hook for column reordering — the whole header cell is the
 * drag handle. Ids are namespaced `col:<id>` so a shared DnD context can tell
 * column drags from row drags. Disabled cells still call the hook (hooks must
 * be unconditional) but return no listeners.
 */
export function useHeaderCellSortable(
  columnId: string,
  disabled: boolean,
): HeaderCellSortable {
  const { setNodeRef, transform, transition, isDragging, listeners, attributes } =
    useSortable({ id: `col:${columnId}`, disabled })
  return {
    setNodeRef,
    style: {
      // Only translate horizontally — a header cell reorders along the row.
      transform: transform
        ? CSS.Transform.toString({ ...transform, y: 0, scaleX: 1, scaleY: 1 })
        : undefined,
      transition,
      // Float the dragged cell above every other header while dragging. The
      // grid's header cells are z-index 20 and pinned headers 22 (see
      // wayd-grid.module.css); this inline value must clear both so the
      // dragged column never slides behind a neighbour or a pinned column.
      zIndex: isDragging ? 30 : undefined,
    },
    isDragging,
    handleProps: { ...listeners, ...attributes },
  }
}

/**
 * Shared `<thead>` cell: renders the header content with click-to-sort
 * (ctrl-click multisort via the table config), the asc/desc sort icon, an
 * optional filter slot, and the column-resize handle (double-click resets).
 *
 * The TanStack `header`/`header.column` props keep a stable identity while
 * their sort/resize state mutates underneath, which breaks React Compiler
 * memoization at BOTH levels: a memoized consumer never re-creates this
 * element (so consumers must carry `'use no memo'` — both grids do), and this
 * cell's own compiled cache would key on `header.column` identity and serve a
 * stale `getIsSorted()` — hence the directive below. (The eslint plugin
 * mis-reports it as unused; runtime fiber inspection shows the cache slots.)
 */
export function GridHeaderCell<T>({
  header,
  resizeGuard,
  classes,
  filterSlot,
  menuSlot,
  thClassName,
  thStyle,
  sortable,
}: GridHeaderCellProps<T>) {
  // eslint-disable-next-line react-compiler/react-compiler -- false-positive "unused directive"; see doc comment
  'use no memo'
  const canSort = header.column.getCanSort()
  const sortState = header.column.getIsSorted()
  const canResize = header.column.getCanResize()

  const sortIcon =
    sortState === 'asc' ? (
      <ArrowUpOutlined />
    ) : sortState === 'desc' ? (
      <ArrowDownOutlined />
    ) : null

  const handleSortClick = canSort
    ? (e: MouseEvent) => {
        if (resizeGuard.consumeResizeClick()) return
        header.column.getToggleSortingHandler()?.(e)
      }
    : undefined

  const handleResizeStart = (e: MouseEvent | TouchEvent) => {
    resizeGuard.beginResize()
    header.getResizeHandler()(e)
  }

  return (
    <th
      // The WHOLE header cell is the drag handle: dnd-kit's PointerSensor only
      // activates after ~8px of movement, so a plain click still falls through
      // to onClick (sort). Interactive children (resizer, filter, menu) stop
      // pointerdown propagation so grabbing them never starts a column drag.
      {...sortable?.handleProps}
      // Anchors autosize's header-label lookup (with the text marker below).
      data-column-id={header.column.id}
      ref={sortable?.setNodeRef}
      className={`${classes.th}${canSort ? ` ${classes.thSortable}` : ''}${
        canResize ? ` ${classes.thResizable}` : ''
      }${sortable ? ` ${classes.thDraggable}` : ''}${
        sortable?.isDragging && classes.thDragging
          ? ` ${classes.thDragging}`
          : ''
      }${thClassName ? ` ${thClassName}` : ''}`}
      style={sortable ? { ...thStyle, ...sortable.style } : thStyle}
      onClick={handleSortClick}
    >
      <span className={classes.thContent}>
        <span className={classes.thText} data-column-header-text="">
          <GridHeaderContent header={header} />
        </span>
        {sortIcon}
        {filterSlot}
        {menuSlot}
      </span>

      {canResize && (
        <span
          role="separator"
          aria-orientation="vertical"
          onMouseDown={handleResizeStart}
          onTouchStart={handleResizeStart}
          onDoubleClick={() => header.column.resetSize()}
          onClick={(e) => e.stopPropagation()}
          // Don't let a resize grab start a column drag (drag listens on the th).
          onPointerDown={(e) => e.stopPropagation()}
          className={`${classes.resizer}${
            header.column.getIsResizing() ? ` ${classes.resizerActive}` : ''
          }`}
        />
      )}
    </th>
  )
}

/**
 * A {@link GridHeaderCell} wired for column-reorder drag. Calls
 * {@link useHeaderCellSortable} (unconditionally — hooks rules) and threads the
 * sortable wiring in; pass `reorderable: false` to render a plain,
 * non-draggable cell (grouped-header grids, the actions column, placeholders).
 * Extracted so the per-header hook lives in its own component instead of a map
 * callback.
 */
export function SortableHeaderCell<T>({
  reorderable,
  ...cellProps
}: GridHeaderCellProps<T> & { reorderable: boolean }) {
  // eslint-disable-next-line react-compiler/react-compiler -- same mutable-header caveat as GridHeaderCell
  'use no memo'
  const sortable = useHeaderCellSortable(cellProps.header.column.id, !reorderable)
  return (
    <GridHeaderCell {...cellProps} sortable={reorderable ? sortable : undefined} />
  )
}
