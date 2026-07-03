import { useState, type ChangeEvent } from 'react'
import {
  type Column,
  type ColumnDef,
  type ColumnFiltersState,
  type ColumnSizingState,
  type SortingState,
  type Table,
  type TableOptions,
  type TableState,
  getCoreRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useReactTable,
} from '@tanstack/react-table'

import { stringContainsFilter } from './grid-filters'

/**
 * Shared grid state: sorting, column filters, column sizing, and the global
 * quick-search value, plus the toolbar wiring derived from them. Created by
 * {@link useGridState} and consumed by {@link useGridTable}.
 *
 * Split from useGridTable so a grid can build state-dependent column
 * definitions (e.g. TreeGrid's column context reads the active filters)
 * between creating the state and creating the table.
 */
export interface GridState {
  sorting: SortingState
  setSorting: React.Dispatch<React.SetStateAction<SortingState>>
  columnFilters: ColumnFiltersState
  setColumnFilters: React.Dispatch<React.SetStateAction<ColumnFiltersState>>
  columnSizing: ColumnSizingState
  setColumnSizing: React.Dispatch<React.SetStateAction<ColumnSizingState>>
  searchValue: string
  setSearchValue: React.Dispatch<React.SetStateAction<string>>
  /** Toolbar search input change handler. */
  onSearchChange: (e: ChangeEvent<HTMLInputElement>) => void
  /** Clears search, sorting, and column filters (plus the grid's own extras). */
  onClearFilters: () => void
  /** True when any search, column filter, or sort is active. */
  hasActiveFilters: boolean
}

export interface UseGridStateOptions {
  /** Called by onClearFilters in addition to the shared clears (e.g. TreeGrid
   *  flushes its debounced filter drafts). */
  onClear?: () => void
}

/**
 * Owns the four shared state slices (sorting, column filters, column sizing,
 * global search) and the toolbar handlers derived from them.
 */
export function useGridState(options?: UseGridStateOptions): GridState {
  const [sorting, setSorting] = useState<SortingState>([])
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([])
  const [columnSizing, setColumnSizing] = useState<ColumnSizingState>({})
  const [searchValue, setSearchValue] = useState('')

  const onSearchChange = (e: ChangeEvent<HTMLInputElement>) => {
    setSearchValue(e.target.value)
  }

  const onClearFilters = () => {
    setSearchValue('')
    setSorting([])
    setColumnFilters([])
    options?.onClear?.()
  }

  const hasActiveFilters =
    !!searchValue || columnFilters.length > 0 || sorting.length > 0

  return {
    sorting,
    setSorting,
    columnFilters,
    setColumnFilters,
    columnSizing,
    setColumnSizing,
    searchValue,
    setSearchValue,
    onSearchChange,
    onClearFilters,
    hasActiveFilters,
  }
}

/** Global quick-search applies to any column with an accessor unless the
 *  column opts out via `enableGlobalFilter: false`. */
const getColumnCanGlobalFilter = <T>(column: Column<T, unknown>): boolean => {
  const colDef = column.columnDef as unknown as {
    enableGlobalFilter?: boolean
    accessorFn?: unknown
    accessorKey?: unknown
  }
  if (colDef.enableGlobalFilter === false) return false
  return Boolean(colDef.accessorFn || colDef.accessorKey)
}

export interface UseGridTableOptions<T> {
  data: T[]
  columns: ColumnDef<T, any>[]
  /** The shared state from {@link useGridState}. */
  gridState: GridState
  /**
   * Grid-specific TanStack options merged over the shared defaults (extra row
   * models, defaultColumn, getSubRows, initialState, …). Cannot override the
   * state/handler wiring, which the hook owns.
   */
  tableOptions?: Omit<
    Partial<TableOptions<T>>,
    'data' | 'columns' | 'state' | 'onStateChange'
  >
  /** Extra controlled state merged into the table state (e.g. a derived
   *  columnVisibility). */
  extraState?: Partial<TableState>
}

/**
 * Creates the TanStack table with the shared WaydGrid configuration: filtered
 * + sorted row models, quick-search via {@link stringContainsFilter},
 * ctrl-click multisort, and onChange column resizing — all wired to the state
 * from {@link useGridState}.
 */
export function useGridTable<T>({
  data,
  columns,
  gridState,
  tableOptions,
  extraState,
}: UseGridTableOptions<T>): Table<T> {
  const {
    sorting,
    setSorting,
    columnFilters,
    setColumnFilters,
    columnSizing,
    setColumnSizing,
    searchValue,
    setSearchValue,
  } = gridState

  return useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getColumnCanGlobalFilter,
    globalFilterFn: stringContainsFilter,
    enableMultiSort: true,
    // Ctrl-click adds a column to the sort; on macOS the conventional key is
    // ⌘ (metaKey), so accept both — matching ag-grid's multiSortKey behavior.
    isMultiSortEvent: (e) => {
      const evt = e as unknown as
        | { ctrlKey?: boolean; metaKey?: boolean }
        | null
      return evt?.ctrlKey === true || evt?.metaKey === true
    },
    enableColumnResizing: true,
    columnResizeMode: 'onChange',
    ...tableOptions,
    state: {
      globalFilter: searchValue,
      sorting,
      columnFilters,
      columnSizing,
      ...extraState,
    },
    onGlobalFilterChange: (value) => setSearchValue(String(value ?? '')),
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    onColumnSizingChange: setColumnSizing,
  })
}
