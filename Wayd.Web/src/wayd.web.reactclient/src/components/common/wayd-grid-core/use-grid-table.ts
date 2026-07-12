import { useState, type ChangeEvent } from 'react'
import {
  type Column,
  type ColumnDef,
  type ColumnFiltersState,
  type ColumnOrderState,
  type ColumnPinningState,
  type ColumnSizingState,
  type SortingState,
  type Table,
  type TableOptions,
  type TableState,
  type VisibilityState,
  getCoreRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useReactTable,
} from '@tanstack/react-table'

import { stringContainsFilter } from './grid-filters'

/**
 * Shared grid state: sorting, column filters, column sizing, user column
 * visibility, column pinning, and the global quick-search value, plus the
 * toolbar wiring derived from them. Created by {@link useGridState} and
 * consumed by {@link useGridTable}.
 *
 * Split from useGridTable so a grid can build state-dependent column
 * definitions (e.g. TreeGrid's column context reads the active filters)
 * between creating the state and creating the table.
 *
 * Every user-adjustable column state slice (columnSizing,
 * userColumnVisibility, columnPinning, columnOrder, sorting) lives here as
 * plain serializable state — useGridColumnStatePersistence
 * (use-grid-persistence.ts) serializes the column-layout slices, so new column
 * state must be added here, not scattered into components.
 */
export interface GridState {
  sorting: SortingState
  setSorting: React.Dispatch<React.SetStateAction<SortingState>>
  columnFilters: ColumnFiltersState
  setColumnFilters: React.Dispatch<React.SetStateAction<ColumnFiltersState>>
  columnSizing: ColumnSizingState
  setColumnSizing: React.Dispatch<React.SetStateAction<ColumnSizingState>>
  /**
   * The USER's show/hide choices (column chooser) — a layer on top of the
   * consumer's reactive `meta.hide` visibility; see
   * {@link mergeColumnVisibility} for how the two combine.
   */
  userColumnVisibility: VisibilityState
  setUserColumnVisibility: React.Dispatch<
    React.SetStateAction<VisibilityState>
  >
  columnPinning: ColumnPinningState
  setColumnPinning: React.Dispatch<React.SetStateAction<ColumnPinningState>>
  /**
   * The user's column order as a list of leaf column ids (TanStack
   * columnOrder). Governs the CENTER section's order; each pinned section is
   * ordered by its columnPinning array instead (TanStack builds pinned
   * sections from the pin arrays, ignoring columnOrder). Ids absent from the
   * list are appended by TanStack — the grid reconciles a persisted order
   * against the live defs so newly added columns land at their def position;
   * see reconcileColumnOrder.
   */
  columnOrder: ColumnOrderState
  setColumnOrder: React.Dispatch<React.SetStateAction<ColumnOrderState>>
  searchValue: string
  setSearchValue: React.Dispatch<React.SetStateAction<string>>
  /** Toolbar search input change handler. */
  onSearchChange: (e: ChangeEvent<HTMLInputElement>) => void
  /** Clears search, sorting, and column filters (plus the grid's own extras). */
  onClearFilters: () => void
  /**
   * Restores the column defs' defaults: sizing, user visibility, pinning, and
   * order (the column menu's Reset Columns). Sort and filters are deliberately
   * NOT reset — that's the toolbar Clear button ({@link onClearFilters}).
   */
  resetColumnState: () => void
  /** True when any search, column filter, or sort is active. */
  hasActiveFilters: boolean
}

export interface UseGridStateOptions {
  /** Called by onClearFilters in addition to the shared clears (e.g. TreeGrid
   *  flushes its debounced filter drafts). */
  onClear?: () => void
  /** Sort applied on mount (ag-grid `sort: 'asc'` equivalent). Read once —
   *  later changes don't reset the user's sorting. */
  initialSorting?: SortingState
}

/** Empty pinning state (nothing pinned) — also what Reset restores. */
const EMPTY_COLUMN_PINNING: ColumnPinningState = { left: [], right: [] }

/**
 * Owns the shared state slices (sorting, column filters, column sizing, user
 * column visibility, column pinning, global search) and the toolbar handlers
 * derived from them.
 */
export function useGridState(options?: UseGridStateOptions): GridState {
  const [sorting, setSorting] = useState<SortingState>(
    options?.initialSorting ?? [],
  )
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([])
  const [columnSizing, setColumnSizing] = useState<ColumnSizingState>({})
  const [userColumnVisibility, setUserColumnVisibility] =
    useState<VisibilityState>({})
  const [columnPinning, setColumnPinning] =
    useState<ColumnPinningState>(EMPTY_COLUMN_PINNING)
  const [columnOrder, setColumnOrder] = useState<ColumnOrderState>([])
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

  const resetColumnState = () => {
    setColumnSizing({})
    setUserColumnVisibility({})
    setColumnPinning(EMPTY_COLUMN_PINNING)
    setColumnOrder([])
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
    userColumnVisibility,
    setUserColumnVisibility,
    columnPinning,
    setColumnPinning,
    columnOrder,
    setColumnOrder,
    searchValue,
    setSearchValue,
    onSearchChange,
    onClearFilters,
    resetColumnState,
    hasActiveFilters,
  }
}

/**
 * Combines the consumer's reactive `meta.hide` visibility with the user's
 * column-chooser choices into the TanStack columnVisibility map.
 *
 * Rules: a column the consumer hides (`meta.hide === true`) is
 * consumer-controlled — it stays hidden no matter what the user chose (it is
 * also excluded from the chooser). For every other column the user's choice
 * wins; absent a user choice, the consumer's `meta.hide: false` (or no entry
 * at all) leaves the column visible.
 */
export function mergeColumnVisibility(
  consumerVisibility: VisibilityState,
  userVisibility: VisibilityState,
): VisibilityState {
  const merged: VisibilityState = { ...userVisibility }
  for (const [id, visible] of Object.entries(consumerVisibility)) {
    if (!visible) merged[id] = false
    else if (!(id in merged)) merged[id] = true
  }
  return merged
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
    columnPinning,
    setColumnPinning,
    columnOrder,
    setColumnOrder,
    searchValue,
    setSearchValue,
  } = gridState

  // eslint-disable-next-line react-hooks/incompatible-library -- the warning is about compiler memoization; WaydGrid, this hook's only consumer, carries 'use no memo' to opt its JSX out, which is where the stale-UI risk actually lives
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
    // Pinning is state-only in TanStack (getVisibleLeafColumns / getVisibleCells /
    // getHeaderGroups reorder pinned columns to the edges); the sticky rendering
    // is the grid's (see column-pinning.ts).
    enableColumnPinning: true,
    ...tableOptions,
    state: {
      globalFilter: searchValue,
      sorting,
      columnFilters,
      columnSizing,
      columnPinning,
      columnOrder,
      ...extraState,
    },
    onGlobalFilterChange: (value) => setSearchValue(String(value ?? '')),
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    onColumnSizingChange: setColumnSizing,
    onColumnPinningChange: setColumnPinning,
    onColumnOrderChange: setColumnOrder,
  })
}
