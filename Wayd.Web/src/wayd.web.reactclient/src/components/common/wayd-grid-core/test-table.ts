import { renderHook } from '@testing-library/react'
import {
  type LegacyColumnDef as ColumnDef,
  type LegacyReactTable as Table,
  getCoreRowModel,
  useLegacyTable,
} from '@tanstack/react-table/legacy'
import type { TableState } from './index'
import type { RowData } from '@tanstack/react-table'

/**
 * Builds a headless TanStack table for assertions in tests.
 *
 * v9 removed the standalone `createTable` these tests used; the legacy shim
 * only exposes the `useLegacyTable` hook, so the table is built inside a
 * throwaway render. State is fully controlled by the caller (no onStateChange)
 * — these are pure structural assertions, nothing dispatches state updates.
 */
export function buildHeadlessTable<T extends RowData>(
  data: T[],
  columns: ColumnDef<T, any>[],
  state: Partial<TableState> = {},
): Table<T> {
  const { result } = renderHook(() =>
    useLegacyTable<T>({
      data,
      columns,
      getCoreRowModel: getCoreRowModel(),
      state: {
        columnSizing: {},
        columnVisibility: {},
        ...state,
      } as TableState,
      renderFallbackValue: null,
    }),
  )
  return result.current
}
