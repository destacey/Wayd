import { renderHook } from '@testing-library/react'
import { useTable } from '@tanstack/react-table'
import type { RowData } from '@tanstack/react-table'

import { waydGridFeatures } from './grid-features'
import type { ColumnDef, Table, TableState } from './index'

/**
 * Builds a headless TanStack table for assertions in tests.
 *
 * v9 removed the standalone `createTable` these tests used and exposes only
 * the `useTable` hook, so the table is built inside a throwaway render. State
 * is fully controlled by the caller — these are pure structural assertions,
 * nothing dispatches state updates.
 */
export function buildHeadlessTable<T extends RowData>(
  data: T[],
  columns: ColumnDef<T, any>[],
  state: Partial<TableState> = {},
): Table<T> {
  const { result } = renderHook(() =>
    useTable<typeof waydGridFeatures, T>({
      features: waydGridFeatures,
      data,
      columns,
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
