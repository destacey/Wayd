import type { ReactNode } from 'react'
import type { RowData } from '@tanstack/react-table'
import type { FilterType } from './filters'

/**
 * Filter option for select-type column filters.
 */
export interface FilterOption {
  label: string
  value: string
}

/**
 * Built-in column types, referenced declaratively via `meta.columnType` (like
 * AG Grid's `type: 'dateOnly'`). The grid resolves each type's display, sort,
 * and filter config from the column-type registry.
 */
export type WaydColumnType = 'yesNo' | 'dateOnly' | 'dateTime'

/**
 * Extended column metadata for Wayd grids, stored in TanStack's
 * `columnDef.meta`.
 *
 * This interface is merged into TanStack's `ColumnMeta` via module augmentation
 * (see the `declare module` block below), so `column.meta.hide`, `.columnType`,
 * `.filterType`, etc. are strongly typed at every call site with no casts.
 */
export interface WaydGridColumnMeta {
  /**
   * Declarative column type (AG Grid `type: 'dateOnly'` style). Applies a
   * preset display/sort/filter config from the registry. Explicit column
   * fields (cell, accessorFn) and meta (filterType) take precedence over the
   * type's defaults.
   */
  columnType?: WaydColumnType
  /**
   * Hide the column while keeping it defined (AG Grid `hide` style). Lets a
   * column stay in one static column literal and be shown/hidden by a flag
   * rather than conditionally pushed. The grid feeds this into TanStack's
   * columnVisibility, so toggling preserves the column's size/state.
   */
  hide?: boolean
  /**
   * Filter type driving the per-column filter popup. Accepts the descriptor
   * filter types (`text` | `number` | `date` | `dateTime` | `set`) directly.
   * Legacy aliases are also accepted for parity with the old inline filter row:
   * `select` → `set`, `numericRange` → `number`. Omit to default to `text`.
   */
  filterType?: FilterType | 'select' | 'numericRange'
  /**
   * Cell text alignment override. By default, columns the grid resolves as
   * numeric (explicit `filterType: 'number'` or all-number sampled data)
   * right-align their BODY cells — headers always stay left-aligned. Set
   * 'left' to opt a numeric column out, or 'right' to force it on.
   */
  align?: 'left' | 'right'
  /** Options for the 'set' (aka legacy 'select') filter type. */
  filterOptions?: FilterOption[]
  /**
   * For a text column, also offer the Excel-style set (checkbox list) filter
   * alongside the text filter — a combined panel (Text Filter expander + set
   * list). One descriptor is active at a time; last-updated wins. The floating
   * input keeps its type-to-Contains behavior.
   */
  filterEnableSet?: boolean
  /**
   * Tooltip shown when hovering the header label (AG Grid `headerTooltip`
   * style). The grid wraps the header content in WaydTooltip automatically —
   * keep `header` a plain string (which CSV export also uses) instead of
   * hand-rolling a Tooltip in a header renderer. Works on grouped-header
   * bands too.
   */
  headerTooltip?: ReactNode
  /** Max AND/OR conditions the popup allows (default 5 — the max; set lower to restrict). Ignored for `set`. */
  maxFilterConditions?: number
  /** Placeholder text for text/numericRange filter inputs. */
  filterPlaceholder?: string
  /**
   * Whether the user may drag this column to reorder it. Default: true.
   * Structural columns that must hold their position (e.g. the row-actions
   * column) set this to `false` so they get no drag grip and reject drops.
   */
  enableReordering?: boolean
  /** Whether to include this column in CSV export. Default: true if the column has an accessor. */
  enableExport?: boolean
  /** Custom CSV formatter for this column's values. */
  exportFormatter?: (value: unknown, row: any) => string
  /** Override the CSV header text for this column. */
  exportHeader?: string
}

/**
 * Augment TanStack's `ColumnMeta` with the Wayd grid fields so `column.meta`
 * is strongly typed everywhere (autocomplete on `meta: { ... }`, no casts).
 * `ColumnMeta` is global, so these optional fields are suggested on any TanStack
 * table in the app — harmless, and the Wayd grids are the only consumers.
 */
declare module '@tanstack/react-table' {
  interface ColumnMeta<TData extends RowData, TValue>
    extends WaydGridColumnMeta {}
}
