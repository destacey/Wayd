'use client'

import { type Cell, type Row, flexRender } from '@tanstack/react-table'
import { GridSortableRow } from './dnd/grid-dnd'
import {
  getPinnedOffsets,
  pinnedCellClassNames,
  pinnedCellStyle,
  type PinnedCellClasses,
} from './column-pinning'

/**
 * CSS-module class names the owning grid supplies for body rows, so the shared
 * markup picks up the grid's own zebra/cell styling. The pinned set is
 * optional — a grid without column pinning just omits it.
 */
export interface GridRowClasses {
  tr: string
  trAlt: string
  td: string
  /** Applied to `td`s of numeric columns (right-aligned cells; headers are
   *  unaffected — alignment is a body-cell concern only). */
  tdNumeric?: string
  /** Sticky/edge classes for pinned columns' `td`s. */
  pinned?: PinnedCellClasses
}

/** The pinned class suffix (starting with a space) + inline style for a body
 *  cell, or empties when the column is unpinned / pinning classes not given. */
const pinnedTdProps = <T,>(
  cell: Cell<T, unknown>,
  classes: GridRowClasses,
): { className: string; style?: React.CSSProperties } => {
  if (!classes.pinned) return { className: '' }
  const offsets = getPinnedOffsets(cell.column)
  if (!offsets) return { className: '' }
  return {
    className: ` ${pinnedCellClassNames(offsets, classes.pinned)}`,
    style: pinnedCellStyle(offsets),
  }
}

/** The numeric-alignment class suffix (starting with a space) for a body
 *  cell, or '' when the column isn't numeric / no class was supplied. */
const numericTdClass = <T,>(
  cell: Cell<T, unknown>,
  classes: GridRowClasses,
  numericColumnIds: ReadonlySet<string> | undefined,
): string =>
  classes.tdNumeric && numericColumnIds?.has(cell.column.id)
    ? ` ${classes.tdNumeric}`
    : ''

export interface FlatGridRowProps<T> {
  row: Row<T>
  /** Display index within the rendered row list (drives zebra striping). */
  index: number
  classes: GridRowClasses
  /** Column ids whose cells right-align (numeric columns); resolved once at
   *  the grid level — see the grid's numericColumnIds. */
  numericColumnIds?: ReadonlySet<string>
}

/**
 * The FLAT form of the row-renderer seam: a plain `<tr>` of the row's visible
 * cells plus the trailing filler cell that keeps the zebra/hover band
 * edge-to-edge. Tree mode supplies its own form (sortable-row wrapper +
 * indent/caret + editable-cell attributes) behind the same seam — do not
 * thread `if (isTree)` through this one.
 *
 * The TanStack `row` prop keeps a stable identity while its state mutates
 * underneath, so every component that renders this must carry `'use no memo'`
 * (the grids do); a directive here alone could not force a memoized parent to
 * re-create the element.
 */
export function FlatGridRow<T>({
  row,
  index,
  classes,
  numericColumnIds,
}: FlatGridRowProps<T>) {
  // eslint-disable-next-line react-compiler/react-compiler -- false-positive "unused directive"; see GridHeaderCell
  'use no memo'
  return (
    <tr
      className={`${classes.tr}${index % 2 === 1 ? ` ${classes.trAlt}` : ''}`}
    >
      {row.getVisibleCells().map((cell) => {
        const pinned = pinnedTdProps(cell, classes)
        return (
          <td
            key={cell.id}
            data-column-id={cell.column.id}
            className={`${classes.td}${numericTdClass(cell, classes, numericColumnIds)}${pinned.className}`}
            style={pinned.style}
          >
            {flexRender(cell.column.columnDef.cell, cell.getContext())}
          </td>
        )
      })}
      {/* Filler data cell keeps the zebra/hover band edge-to-edge. */}
      <td aria-hidden="true" className={classes.td} />
    </tr>
  )
}

export interface SortableFlatGridRowProps<T> extends FlatGridRowProps<T> {
  /** The row's data id (not TanStack's row.id) — the dnd-kit sortable id. */
  nodeId: string
  /** Whether this row is currently being dragged. */
  isDragging: boolean
  /** Whether DnD is enabled for this row. */
  isDragEnabled: boolean
}

/**
 * The flat form wrapped in the dnd-kit sortable row ({@link GridSortableRow})
 * for flat row-reorder DnD. Rendered (with dragging possibly disabled) for
 * every row whenever the grid has `onRowReorder`, so drag-handle cells can
 * always reach the `useGridDragHandle` context.
 *
 * Same memoization caveat as {@link FlatGridRow}: consumers must carry
 * `'use no memo'`.
 */
export function SortableFlatGridRow<T>({
  row,
  index,
  classes,
  numericColumnIds,
  nodeId,
  isDragging,
  isDragEnabled,
}: SortableFlatGridRowProps<T>) {
  // eslint-disable-next-line react-compiler/react-compiler -- false-positive "unused directive"; see GridHeaderCell
  'use no memo'
  return (
    <GridSortableRow
      nodeId={nodeId}
      isDragEnabled={isDragEnabled}
      isDragging={isDragging}
      className={`${classes.tr}${index % 2 === 1 ? ` ${classes.trAlt}` : ''}`}
    >
      {row.getVisibleCells().map((cell) => {
        const pinned = pinnedTdProps(cell, classes)
        return (
          <td
            key={cell.id}
            data-column-id={cell.column.id}
            className={`${classes.td}${numericTdClass(cell, classes, numericColumnIds)}${pinned.className}`}
            style={pinned.style}
          >
            {flexRender(cell.column.columnDef.cell, cell.getContext())}
          </td>
        )
      })}
      {/* Filler data cell keeps the zebra/hover band edge-to-edge. */}
      <td aria-hidden="true" className={classes.td} />
    </GridSortableRow>
  )
}

/** Additional row classes tree mode needs on top of the flat set. */
export interface TreeGridRowClasses extends GridRowClasses {
  trEditable: string
  trSelected: string
  editableCell: string
}

export interface TreeGridRowProps<T> {
  row: Row<T>
  /** Display index within the rendered row list (drives zebra striping). */
  index: number
  classes: TreeGridRowClasses
  /** Column ids whose cells right-align (numeric columns). */
  numericColumnIds?: ReadonlySet<string>
  /** The row's data id (not TanStack's row.id). */
  nodeId: string
  /** Whether this row is selected for inline editing. */
  isSelected: boolean
  /** Whether this row is currently being dragged. */
  isDragging: boolean
  /** Whether DnD is enabled for this row. */
  isDragEnabled: boolean
  /** Whether the grid is editable at all (cursor affordance). */
  canEdit: boolean
  /** Column ids editable for the selected row. */
  editableColumns: string[]
  onRowClick: (e: React.MouseEvent, rowId: string) => void
  onCellClick: (e: React.MouseEvent) => void
}

/**
 * The TREE form of the row-renderer seam: wraps the row in the dnd-kit
 * sortable context ({@link GridSortableRow}) and adds the editing affordances
 * (`data-cell-id` per cell, editable-cell styling on the selected row).
 * Indentation and the expand caret are rendered by the caller's columns
 * (via `row.depth` / `row.getCanExpand()`), not here.
 *
 * Same memoization caveat as {@link FlatGridRow}: consumers must carry
 * `'use no memo'`.
 */
export function TreeGridRow<T>({
  row,
  index,
  classes,
  numericColumnIds,
  nodeId,
  isSelected,
  isDragging,
  isDragEnabled,
  canEdit,
  editableColumns,
  onRowClick,
  onCellClick,
}: TreeGridRowProps<T>) {
  // eslint-disable-next-line react-compiler/react-compiler -- false-positive "unused directive"; see GridHeaderCell
  'use no memo'
  return (
    <GridSortableRow
      nodeId={nodeId}
      isDragEnabled={isDragEnabled}
      isDragging={isDragging}
      className={`${classes.tr}${canEdit ? ` ${classes.trEditable}` : ''}${
        index % 2 === 1 ? ` ${classes.trAlt}` : ''
      }${isSelected ? ` ${classes.trSelected}` : ''}`}
      onClick={(e) => onRowClick(e, nodeId)}
    >
      {row.getVisibleCells().map((cell) => {
        const isEditableCell =
          isSelected && editableColumns.includes(cell.column.id)
        const pinned = pinnedTdProps(cell, classes)

        return (
          <td
            key={cell.id}
            data-cell-id={`${nodeId}-${cell.column.id}`}
            data-column-id={cell.column.id}
            className={`${classes.td}${numericTdClass(cell, classes, numericColumnIds)}${
              isEditableCell ? ` ${classes.editableCell}` : ''
            }${pinned.className}`}
            style={pinned.style}
            onClick={onCellClick}
          >
            {flexRender(cell.column.columnDef.cell, cell.getContext())}
          </td>
        )
      })}
      {/* Filler data cell keeps the zebra/hover band edge-to-edge. */}
      <td aria-hidden="true" className={classes.td} />
    </GridSortableRow>
  )
}
