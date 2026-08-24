import type { ColumnDef, Row, SortingState } from '../wayd-grid-core'
import type { OnChangeFn, RowSelectionState } from '@tanstack/react-table'
import type { FormInstance } from 'antd'
import type { DraftItem } from '../wayd-grid-core/draft-utils'
import type { MoveValidator } from '../wayd-grid-core/dnd/tree-projection'

// Column meta + filter option types moved to the shared grid core (including
// the TanStack ColumnMeta module augmentation); re-exported so wayd-grid
// consumers keep a single import surface.
export type {
  FilterOption,
  WaydColumnType,
  WaydGridColumnMeta,
} from '../wayd-grid-core/types'
import type { RowData } from '@tanstack/react-table'

/**
 * Context passed to the `columns` and `leftSlot` function props.
 * Provides editing, DnD, and draft state so domain code can build columns
 * reactively (e.g. a Create button that needs `canCreateDraft`).
 */
export interface GridColumnContext {
  selectedRowId: string | null
  handleKeyDown: (
    e: React.KeyboardEvent,
    rowId: string,
    columnId: string,
  ) => Promise<void>
  /** Creates an `onInputKeyDown` handler for antd Select that prevents Tab
   *  from triggering rc-select's built-in "select on Tab" behavior while
   *  still forwarding the event to handleKeyDown for navigation. */
  createSelectInputKeyDown: (
    rowId: string,
    columnId: string,
  ) => React.KeyboardEventHandler<HTMLInputElement | HTMLTextAreaElement>
  getFieldError: (fieldName: string) => string | undefined
  editableColumns: string[]
  isDragEnabled: boolean
  canCreateDraft: boolean
  addDraftAtRoot: () => string | null
  addDraftAsChild: (parentId: string) => string | null
}

/**
 * Inline editing configuration for WaydGrid consumers.
 * The grid fills in `data`, `tableWrapperClassName`, `fieldErrors`,
 * `setFieldErrors`, and `onCancelDraft` internally.
 */
export interface GridInlineEditingConfig<T extends RowData> {
  canEdit: boolean
  form: FormInstance
  editableColumnIds: string[] | ((selectedRowId: string | null) => string[])
  onSave: (rowId: string, updates: Record<string, any>) => Promise<boolean>
  getFormValues: (rowId: string, data: T[]) => Record<string, any>
  computeChanges: (
    rowId: string,
    formValues: Record<string, any>,
    data: T[],
  ) => Record<string, any> | null
  validateFields?: (
    rowId: string,
    formValues: Record<string, any>,
  ) => Record<string, string>
  cellIdColumnMatchOrder: readonly string[]
  draftPrefix?: string
}

/**
 * Attaches a chart pane to the RIGHT of the grid, sharing the grid's row
 * geometry (one virtualizer, one vertical scroller — so bars can never drift
 * from their rows). Domain-agnostic: WaydGrid knows nothing about dates. The
 * consumer supplies the header (e.g. a date axis) and a per-row renderer that
 * receives each row plus its resolved vertical position, and returns the bar(s)
 * for that row — enabling a Gantt "chart mode" on any grid (list stays the
 * spine; the chart is the collapsible companion). Omit for a plain grid.
 */
export interface WaydGridRightPane<T extends RowData> {
  /** Pane header, aligned to the grid header height (e.g. the date axis). It
   *  should manage its own horizontal scroll in sync with the body track. */
  header?: React.ReactNode
  /** Default pane width, px. Resizable via the divider. */
  defaultWidth?: number
  /** Minimum pane width, px. Default 200. */
  minWidth?: number
  /** Called (on resize end) with the new pane width, px, for persistence. */
  onWidthChange?: (width: number) => void
  /**
   * Renders the bar(s) for one row. `top`/`height` are the row's resolved
   * geometry from the SAME virtualizer the grid rows use, so bars stay aligned.
   * Content is absolutely positioned within the pane's scrolling track: place a
   * bar at `top` and center it vertically with `(height - barHeight) / 2`.
   */
  renderRow: (ctx: { row: Row<T>; top: number; height: number }) => React.ReactNode
  /**
   * Optional chart-wide layer rendered BEHIND all rows, spanning the full canvas
   * (e.g. vertical gridlines). Receives the total content height so it can fill
   * the whole track. Positioned absolutely within the scrolling canvas.
   */
  renderBackground?: (ctx: { totalHeight: number }) => React.ReactNode
  /**
   * Optional wheel handler over the chart, called before the grid forwards the
   * wheel to its vertical scroller. Receives the NATIVE WheelEvent from a
   * non-passive listener, so `preventDefault()` works (e.g. to block the
   * browser's Ctrl/Cmd+wheel page zoom). Return true if you handled it to
   * suppress the default vertical-scroll forwarding.
   */
  onWheel?: (e: WheelEvent) => boolean | void
}

/**
 * Props for the WaydGrid component — flat by default; provide `getSubRows`
 * to turn on tree mode (expansion, indentation via caller columns,
 * filterFromLeafRows, and — when configured — reparenting DnD, inline
 * editing, and draft rows).
 */
export interface WaydGridProps<T extends RowData> {
  /**
   * Row data. In tree mode, the root nodes (children come from getSubRows).
   * May be undefined while loading (e.g. straight from a query hook) — treated
   * as an empty grid, matching the old ag-grid `rowData` tolerance.
   */
  data: T[] | undefined
  /** Loading state. */
  isLoading?: boolean
  /**
   * Column definitions. Can be a static array or a function that receives
   * editing/DnD/draft context and returns columns.
   */
  columns:
    | ColumnDef<T, any>[]
    | ((context: GridColumnContext) => ColumnDef<T, any>[])

  // -- Toolbar --
  onRefresh?: () => Promise<any> | void
  /**
   * Slot for domain-specific actions rendered on the left of the toolbar.
   * Can be a ReactNode or a function receiving context (useful for Create
   * buttons that need `canCreateDraft` / `addDraftAtRoot`).
   */
  leftSlot?:
    | React.ReactNode
    | ((context: GridColumnContext) => React.ReactNode)
  /** Content rendered inside the help popover. */
  helpContent?: React.ReactNode
  /** Slot for actions rendered just before the export/help group (a divider
   *  separates it from export). For grid-specific toggles (e.g. a Gantt chart). */
  actionsSlot?: React.ReactNode
  /** Slot for actions rendered on the far right of the toolbar. */
  rightSlot?: React.ReactNode
  emptyMessage?: string
  /** Fixed height in pixels. When omitted, the grid auto-sizes to fill the remaining viewport height. */
  height?: number
  /** File name prefix for CSV export (e.g., 'projects'). */
  csvFileName?: string

  // -- Behavior toggles --
  /**
   * Sort applied on mount (ag-grid `sort: 'asc'` equivalent), e.g.
   * `[{ id: 'done', desc: true }]`. Read once — later changes don't reset
   * the user's sorting.
   */
  initialSorting?: SortingState
  /**
   * Preset for the two situations grids are used in.
   *
   * `'advanced'` (default) is the data-exploration surface: toolbar, filters,
   * column menu, and a body that fills the remaining viewport height.
   *
   * `'simple'` is a grid inside a record section — read-and-click over a small,
   * bounded set. No toolbar or filters (the section heading above already says
   * what it is), and the height fits the rows rather than running to the bottom
   * of the page. Individual flags still override the preset.
   */
  variant?: 'simple' | 'advanced'
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

  // -- Row identity --
  /** Stable row id (required for `onRowReorder`). Falls back to
   *  `row.original.id`, then TanStack's index-based id. */
  getRowId?: (row: T) => string

  // -- Grid state --
  /**
   * Opt-in localStorage persistence of the user's column layout (sizing,
   * show/hide choices, pinning) under `wayd-grid:{key}:v1`. Keys are per page
   * context — stable, human-readable, route-independent kebab-case (e.g.
   * 'ppm-projects', 'team-backlog'); shared grid components should expose
   * this as a pass-through prop so each page site supplies its own. Omit for
   * no persistence. Sorting/filters/search are deliberately not persisted.
   */
  persistStateKey?: string
  /**
   * Fires with the displayed rows (post filter + sort, in display order)
   * whenever that set changes — including on mount. For consumers deriving
   * external UI (e.g. a chart) from the grid state.
   */
  onDisplayedRowsChange?: (rows: T[]) => void

  /**
   * Controlled TanStack row-selection state (a map of row id -> selected).
   * Supplying it turns on the grid's selection APIs -- `row.getIsSelected()`
   * and `row.toggleSelected()` per row, `table.getIsAllRowsSelected()`,
   * `table.getIsSomeRowsSelected()`, and
   * `table.getToggleAllRowsSelectedHandler()` for a header checkbox -- so
   * consumers render their own checkbox column against the table instead of
   * tracking selection themselves. Select-all is scoped to the FILTERED rows;
   * selections of rows that later filter out are retained, matching TanStack.
   *
   * Note `getIsSomeRowsSelected()` means "at least one", so an indeterminate
   * header checkbox needs `!getIsAllRowsSelected() && getIsSomeRowsSelected()`.
   */
  rowSelection?: RowSelectionState
  /** Change handler for {@link rowSelection}. Required to make it editable. */
  onRowSelectionChange?: OnChangeFn<RowSelectionState>

  // -- Flat row reorder (enabled when provided) --
  /**
   * Called after a row-drag drop with the displayed rows in post-drop order.
   * Dragging auto-disables while sorted/filtered/searched (the displayed
   * order wouldn't be the data order), loading, or editing — column functions
   * read `context.isDragEnabled` to render their drag handle accordingly.
   */
  onRowReorder?: (event: RowReorderEvent<T>) => void | Promise<void>

  // -- Tree mode (turned on by providing getSubRows) --
  /** How to extract child rows. Presence of this prop enables tree mode. */
  getSubRows?: (row: T) => T[] | undefined

  // -- DnD (tree mode; enabled when onNodeMove is provided) --
  enableDragAndDrop?: boolean
  /** Called when a node is moved via DnD. Receives the node ID, new parent ID, and order. */
  onNodeMove?: (
    nodeId: string,
    parentId: string | null,
    order: number,
    overNodeId?: string,
    overIndex?: number,
  ) => Promise<void>
  /** Called when a DnD move is rejected by the projection/validator. */
  onMoveRejected?: (reason: string) => void
  /** Domain-specific move rules; rows must be tree nodes ({ id, children }). */
  moveValidator?: MoveValidator<any>

  // -- Inline editing (enabled when editingConfig is provided) --
  editingConfig?: GridInlineEditingConfig<T>
  /** External field-level validation errors (e.g., from API 422 responses). */
  fieldErrors?: Record<string, string>
  /** Called when field errors change (cleared on successful validation, set on failure). */
  onFieldErrorsChange?: (errors: Record<string, string>) => void

  // -- Drafts (tree mode; enabled when createDraftNode is provided) --
  /** Factory to create a full row node from a draft item. */
  createDraftNode?: (draft: DraftItem) => T
  /** Called when a draft is cancelled (e.g., Escape key on a draft row). */
  onDraftCancelled?: (draftId: string) => void
  /** Called when the internal draft list changes. */
  onDraftsChange?: (drafts: DraftItem[]) => void

  // -- Right chart pane (Gantt "chart mode"; enabled when provided) --
  /**
   * Attach a bar-chart pane to the right of the grid that shares the grid's row
   * geometry (see {@link WaydGridRightPane}). Turns the list into a Gantt while
   * keeping every grid feature. Omit for a plain grid — the body structure is
   * completely unchanged when this is absent.
   */
  rightPane?: WaydGridRightPane<T>
}

/** Payload for {@link WaydGridProps.onRowReorder}. */
export interface RowReorderEvent<T extends RowData> {
  /** All displayed rows in their post-drop order. */
  orderedData: T[]
  /** The dragged row's id. */
  activeId: string
  /** The dragged row's displayed index before the drop. */
  fromIndex: number
  /** The dragged row's displayed index after the drop. */
  toIndex: number
}

/**
 * Handle exposed by WaydGrid via ref.
 */
export interface WaydGridHandle {
  /** The underlying TanStack table instance. */
  table: any
  /** The currently selected row ID (from the editing hook), or null. */
  selectedRowId: string | null
  /** The displayed (post filter + sort) rows' data, in display order. */
  getDisplayedRows: () => unknown[]
}
