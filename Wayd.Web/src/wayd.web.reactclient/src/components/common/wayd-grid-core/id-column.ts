import type { ColumnDef } from './index'
import type { RowData } from '@tanstack/react-table'

/** Column id (and accessor key) of the auto-injected identifier column. */
export const ID_COLUMN_ID = 'id'

/**
 * Lets a user unhide record ids and export them, since imports reference
 * existing records by `Id` and nothing else in the UI surfaces one.
 *
 * Uses `hiddenByDefault`, not `hide`: `hide` would keep it out of Choose
 * Columns, leaving no way to reveal it.
 */
export function createIdColumn<T extends RowData>(): ColumnDef<T, any> {
  return {
    id: ID_COLUMN_ID,
    accessorKey: ID_COLUMN_ID,
    header: 'Id',
    size: 300,
    // A hidden column that still matched the quick search would surface rows
    // for no reason the user can see.
    enableGlobalFilter: false,
    meta: {
      hiddenByDefault: true,
      exportHeader: 'Id',
    },
  } as ColumnDef<T, any>
}

/** Whether any def in the tree (recursing bands) already claims `id`. */
export function hasIdColumn<T extends RowData>(
  columns: readonly ColumnDef<T, any>[],
): boolean {
  return columns.some((col) => {
    const children = (col as { columns?: ColumnDef<T, any>[] }).columns
    if (children && hasIdColumn(children)) return true
    const id =
      col.id ?? (col as { accessorKey?: string | number }).accessorKey?.toString()
    return id === ID_COLUMN_ID
  })
}

/** Reads the first non-null row: `data` may be sparse or still loading. */
export function rowsHaveId(data: readonly unknown[] | undefined): boolean {
  if (!data?.length) return false
  const first = data.find((row) => row != null && typeof row === 'object')
  if (!first) return false
  const value = (first as { id?: unknown }).id
  if (typeof value === 'string') return value.length > 0
  return typeof value === 'number'
}

/**
 * Returns `columns` unchanged when nothing is added — callers memoize on the
 * result, so a fresh array every render would rebuild the table's headers.
 */
export function withIdColumn<T extends RowData>(
  columns: ColumnDef<T, any>[],
  data: readonly unknown[] | undefined,
  enabled: boolean,
): ColumnDef<T, any>[] {
  if (!enabled) return columns
  if (!rowsHaveId(data)) return columns
  if (hasIdColumn(columns)) return columns
  return [...columns, createIdColumn<T>()]
}
