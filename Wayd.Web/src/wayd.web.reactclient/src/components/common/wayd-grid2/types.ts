import type { ColumnDef } from '@tanstack/react-table'

// Column meta + filter option types moved to the shared grid core (including
// the TanStack ColumnMeta module augmentation); re-exported so wayd-grid2
// consumers keep a single import surface.
export type {
  FilterOption,
  WaydColumnType,
  WaydGridColumnMeta,
} from '../wayd-grid-core/types'

/**
 * Props for the WaydGrid2 component (flat data grid).
 */
export interface WaydGrid2Props<T> {
  /** Row data. */
  data: T[]
  /** Loading state. */
  isLoading?: boolean
  /** Column definitions. */
  columns: ColumnDef<T, any>[]

  // -- Toolbar --
  onRefresh?: () => Promise<any> | void
  /** Slot for domain-specific actions rendered on the left of the toolbar (e.g., a Create button). */
  leftSlot?: React.ReactNode
  /** Content rendered inside the help popover. */
  helpContent?: React.ReactNode
  /** Slot for actions rendered on the far right of the toolbar. */
  rightSlot?: React.ReactNode
  emptyMessage?: string
  /** Fixed height in pixels. When omitted, the grid auto-sizes to fill the remaining viewport height. */
  height?: number
  /** File name prefix for CSV export (e.g., 'projects'). */
  csvFileName?: string

  // -- Behavior toggles --
  /** Whether to show the global search input. Default: true. */
  includeGlobalSearch?: boolean
  /** Whether to show the CSV export button. Default: true. */
  includeExportButton?: boolean
  /**
   * Whether columns are filterable at all — enables the per-column filter popup
   * (opened from the header filter icon). Default: true. When false, no column
   * filtering UI is shown regardless of `includeFloatingFilters`.
   */
  includeColumnFilters?: boolean
  /**
   * Whether to show the inline floating-filter row beneath the header — a
   * compact single-condition editor per column, in addition to the popup.
   * Default: true. Ignored when `includeColumnFilters` is false.
   */
  includeFloatingFilters?: boolean
}

/**
 * Handle exposed by WaydGrid2 via ref.
 */
export interface WaydGrid2Handle {
  /** The underlying TanStack table instance. */
  table: any
}
