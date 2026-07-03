'use client'

import { type Row, flexRender } from '@tanstack/react-table'
import { GridSortableRow } from './dnd/grid-dnd'

/**
 * CSS-module class names the owning grid supplies for body rows, so the shared
 * markup picks up the grid's own zebra/cell styling.
 */
export interface GridRowClasses {
  tr: string
  trAlt: string
  td: string
}

export interface FlatGridRowProps<T> {
  row: Row<T>
  /** Display index within the rendered row list (drives zebra striping). */
  index: number
  classes: GridRowClasses
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
export function FlatGridRow<T>({ row, index, classes }: FlatGridRowProps<T>) {
  // eslint-disable-next-line react-compiler/react-compiler -- false-positive "unused directive"; see GridHeaderCell
  'use no memo'
  return (
    <tr
      className={`${classes.tr}${index % 2 === 1 ? ` ${classes.trAlt}` : ''}`}
    >
      {row.getVisibleCells().map((cell) => (
        <td
          key={cell.id}
          data-column-id={cell.column.id}
          className={classes.td}
        >
          {flexRender(cell.column.columnDef.cell, cell.getContext())}
        </td>
      ))}
      {/* Filler data cell keeps the zebra/hover band edge-to-edge. */}
      <td aria-hidden="true" className={classes.td} />
    </tr>
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

        return (
          <td
            key={cell.id}
            data-cell-id={`${nodeId}-${cell.column.id}`}
            data-column-id={cell.column.id}
            className={`${classes.td}${
              isEditableCell ? ` ${classes.editableCell}` : ''
            }`}
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
