import type { ColumnDef } from '@tanstack/react-table'
import dayjs from 'dayjs'

import type { WaydColumnType, WaydGridColumnMeta } from './types'

// WaydColumnType keys the registry below.

/**
 * WaydGrid2 column types — a declarative registry mirroring AG Grid's
 * `columnTypes` + `type: 'dateOnly'` ergonomics. A column opts into a type via
 * `meta: { columnType: 'dateOnly' }`; the grid resolves the type's display,
 * sort, and filter config from this registry (see {@link applyColumnType}).
 */

const DATE_ONLY_FORMAT = 'MMM D, YYYY'
const DATE_TIME_FORMAT = 'MMM D, YYYY hh:mm A'

/** Formats a date-ish value with the grid's dateOnly display format ('' when empty). */
export const formatDateOnly = (value: unknown): string =>
  value ? dayjs(value as Date | string).format(DATE_ONLY_FORMAT) : ''

/** Formats a date-ish value with the grid's dateTime display format ('' when empty).
 * Exported for columns that need the standard format inside a custom cell
 * (e.g. an empty-value fallback like "Never"). */
export const formatDateTime = (value: unknown): string =>
  value ? dayjs(value as Date | string).format(DATE_TIME_FORMAT) : ''

/** Display/filter strings for the yes-no type. */
export const YES = 'Yes'
export const NO = 'No'

/**
 * A column type's contribution to a ColumnDef. `filterType`/`filterOptions`
 * merge into the column's meta; the rest override the column's own fields when
 * the column doesn't already set them.
 */
interface ColumnTypeDef<T> {
  /** Transforms the raw accessed value for display + (for set types) filtering. */
  accessorFn?: (raw: unknown) => unknown
  cell?: NonNullable<ColumnDef<T, unknown>['cell']>
  filterType?: WaydGridColumnMeta['filterType']
  filterOptions?: WaydGridColumnMeta['filterOptions']
  /** Default column width; used only when the column doesn't set its own `size`. */
  size?: number
}

/** Default width for a yes-no column (compact — the content is just Yes/No). */
export const YES_NO_COLUMN_SIZE = 110

/**
 * The registry. Each entry is a factory so the returned closures are per-column
 * (React-Compiler-friendly — no shared mutable identities).
 *
 * - **yesNo**: boolean → "Yes"/"No" for BOTH display and filter. The accessor
 *   returns the display string so the `set` filter matches "Yes"/"No" (not
 *   true/false). Null/undefined → blank / excluded from the set.
 * - **dateOnly / dateTime**: display formatted via `cell`, but the accessor
 *   keeps the RAW value so chronological sort is correct and the date/dateTime
 *   descriptor filter (which parses dates) works. Mirrors AG Grid, where
 *   `valueFormatter` formats while the underlying value drives sort/filter.
 */
const registry: { [K in WaydColumnType]: <T>() => ColumnTypeDef<T> } = {
  yesNo: <T>(): ColumnTypeDef<T> => ({
    accessorFn: (raw) => {
      if (raw === null || raw === undefined) return ''
      return raw ? YES : NO
    },
    cell: ({ getValue }) => (getValue() as string) ?? '',
    filterType: 'set',
    filterOptions: [
      { label: YES, value: YES },
      { label: NO, value: NO },
    ],
    // Booleans use the same Excel-style set panel as any other set column
    // (False/True as a 2-item checkbox list) — consistent with AG Grid.
    size: YES_NO_COLUMN_SIZE,
  }),
  dateOnly: <T>(): ColumnTypeDef<T> => ({
    // Accessor left undefined → raw value flows through for sorting/filtering.
    cell: ({ getValue }) => formatDateOnly(getValue()),
    filterType: 'date',
  }),
  dateTime: <T>(): ColumnTypeDef<T> => ({
    cell: ({ getValue }) => formatDateTime(getValue()),
    filterType: 'dateTime',
  }),
}

/**
 * Reads the raw accessed value for a column, honoring `accessorFn` then
 * `accessorKey`. Used to feed a column type's value transform.
 */
const readRaw = <T>(col: ColumnDef<T, unknown>, row: T): unknown => {
  const anyCol = col as {
    accessorFn?: (row: T, index: number) => unknown
    accessorKey?: string | number
  }
  if (typeof anyCol.accessorFn === 'function') return anyCol.accessorFn(row, 0)
  if (anyCol.accessorKey != null) {
    return (row as Record<string, unknown>)[anyCol.accessorKey as string]
  }
  return undefined
}

/**
 * Applies the column's declared `meta.columnType` to a ColumnDef, returning a
 * new def. No-op when the column declares no (or an unknown) type.
 *
 * Precedence — explicit column config wins over the type's defaults:
 * - The type's value transform wraps the column's existing accessor (so the
 *   raw value is read first, then mapped for display/filtering).
 * - `cell`, `size`, and meta `filterType`/`filterOptions` are only filled in
 *   where the column didn't already set them.
 */
export const applyColumnType = <T>(
  col: ColumnDef<T, unknown>,
): ColumnDef<T, unknown> => {
  const meta = col.meta
  const columnType = meta?.columnType
  if (!columnType || !(columnType in registry)) return col

  const type = registry[columnType]<T>()
  const next = { ...col } as ColumnDef<T, unknown> & {
    accessorFn?: (row: T, index: number) => unknown
    accessorKey?: unknown
  }

  // Value transform: wrap the column's existing accessor so the type maps the
  // raw value. Set types (yesNo) return the display string so the set filter
  // matches it; date types leave accessorFn undefined (raw value flows through).
  if (type.accessorFn) {
    const transform = type.accessorFn
    next.accessorFn = (row: T) => transform(readRaw(col, row))
    // accessorFn takes over; drop accessorKey so TanStack uses the fn.
    delete next.accessorKey
  }

  if (type.cell && col.cell === undefined) {
    next.cell = type.cell
  }

  if (type.size !== undefined && col.size === undefined) {
    next.size = type.size
  }

  next.meta = {
    ...(meta ?? {}),
    filterType: meta?.filterType ?? type.filterType,
    filterOptions: meta?.filterOptions ?? type.filterOptions,
  } satisfies WaydGridColumnMeta

  return next
}
