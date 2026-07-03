'use client'

import { type Row, flexRender } from '@tanstack/react-table'

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
