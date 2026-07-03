'use client'

import { useRef, type MouseEvent, type ReactNode, type TouchEvent } from 'react'
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons'
import { type Header, flexRender } from '@tanstack/react-table'
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
}

export interface GridHeaderCellProps<T> {
  header: Header<T, unknown>
  /** The grid's {@link useResizeClickGuard}, so the click that ends a column
   *  resize doesn't toggle the sort. */
  resizeGuard: ResizeClickGuard
  classes: GridHeaderCellClasses
  /** Optional filter affordance rendered after the sort icon (e.g. the
   *  wayd-grid2 filter popover trigger). */
  filterSlot?: ReactNode
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
      className={`${classes.th}${canSort ? ` ${classes.thSortable}` : ''}${
        canResize ? ` ${classes.thResizable}` : ''
      }`}
      onClick={handleSortClick}
    >
      <span className={classes.thContent}>
        <span className={classes.thText}>
          <GridHeaderContent header={header} />
        </span>
        {sortIcon}
        {filterSlot}
      </span>

      {canResize && (
        <span
          role="separator"
          aria-orientation="vertical"
          onMouseDown={handleResizeStart}
          onTouchStart={handleResizeStart}
          onDoubleClick={() => header.column.resetSize()}
          onClick={(e) => e.stopPropagation()}
          className={`${classes.resizer}${
            header.column.getIsResizing() ? ` ${classes.resizerActive}` : ''
          }`}
        />
      )}
    </th>
  )
}
