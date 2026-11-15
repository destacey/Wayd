import type { RowData } from '@tanstack/react-table'

import type { ColumnDef } from '../index'

const leafId = <T extends RowData>(col: ColumnDef<T, unknown>) =>
  col.id ?? (col as { accessorKey?: string | number }).accessorKey?.toString()

/**
 * Turns off sorting and filtering on every column the server cannot sort or
 * filter by: a column whose id is not one of `fields`, the server's field map.
 * A column that already turned either off keeps it off. Bands recurse to their
 * leaves.
 */
export function restrictToServerFields<T extends RowData>(
  columns: ColumnDef<T, unknown>[],
  fields: readonly string[],
): ColumnDef<T, unknown>[] {
  return columns.map((col) => {
    const children = (col as { columns?: ColumnDef<T, unknown>[] }).columns
    if (children) {
      return {
        ...col,
        columns: restrictToServerFields(children, fields),
      } as ColumnDef<T, unknown>
    }
    const id = leafId(col)
    if (id !== undefined && fields.includes(id)) return col
    return { ...col, enableSorting: false, enableColumnFilter: false }
  })
}
