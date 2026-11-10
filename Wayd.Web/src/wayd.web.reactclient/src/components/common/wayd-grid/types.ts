import type { ColumnDef, Row, SortingState } from '../wayd-grid-core'
import type { OnChangeFn, RowSelectionState } from '@tanstack/react-table'
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

/** What {@link WaydGridProps.renderGroupHeader} is handed for one group row. */
export interface GroupHeaderContext<T extends RowData> {
  /** The grouped column this header is a value of. */
  columnId: string
  /** The group's value, as the column's accessor returned it. */
  value: unknown
  /** Every data row under this heading, through any nested groups. */
  leafRows: T[]
  /** Nesting depth: 0 for the outermost grouping. */
  depth: number
}

/**
 * Context passed to the `columns` and `leftSlot` function props.
 * Provides DnD and draft state so domain code can build columns reactively
 * (e.g. a Create button that needs `canCreateDraft`).
 */
export interface GridColumnContext {
  /** Whether rows can be dragged right now; drag handles grey out when not. */
  isDragEnabled: boolean
  /** Whether a draft row can be added now: cell editing with
   *  `createDraftNode` and `onCreate`, no draft already open, nothing loading. */
  canCreateDraft: boolean
  /** Adds a draft row at the root and opens it for editing; the draft's id,
   *  or null when none was added. */
  addDraftAtRoot: () => string | null
  /** Adds a draft row under a parent, expanding it; the draft's id, or null. */
  addDraftAsChild: (parentId: string) => string | null
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
  renderRow: (ctx: {
    row: Row<T>
    top: number
    height: number
  }) => React.ReactNode
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
 * filterFromLeafRows, and — when configured — reparenting DnD, cell
 * editing, and draft rows).
 */
export interface WaydGridProps<T extends RowData> {
  /**
   * Row data. In tree mode, the root nodes (children come from getSubRows).
   * May be undefined while loading (e.g. straight from a query hook) — treated
   * as an empty grid, matching the old ag-grid `rowData` tolerance.
   */
  data: T[] | undefined
  /**
   * The grid's accessible name, announced when a screen reader enters it.
   * Give one when the page has more than one grid or no heading naming it.
   */
  'aria-label'?: string
  /** Loading state. */
  isLoading?: boolean
  /**
   * Column definitions. Can be a static array or a function that receives
   * editing/DnD/draft context and returns columns.
   */
  columns:
    ColumnDef<T, any>[] | ((context: GridColumnContext) => ColumnDef<T, any>[])
  /**
   * Appends a hidden `Id` column when the rows carry an `id` and the columns
   * don't already define one, so a user can unhide it via Choose Columns and
   * export the ids an import file needs. On by default; pass `false` for grids
   * whose `id` is a client-side row key rather than a Wayd record id.
   */
  includeIdColumn?: boolean

  // -- Toolbar --
  onRefresh?: () => Promise<any> | void
  /**
   * Slot for domain-specific actions rendered on the left of the toolbar.
   * Can be a ReactNode or a function receiving context (useful for Create
   * buttons that need `canCreateDraft` / `addDraftAtRoot`).
   */
  leftSlot?: React.ReactNode | ((context: GridColumnContext) => React.ReactNode)
  /** Content rendered inside the help popover, in place of the generated
   *  keyboard help. */
  helpContent?: React.ReactNode
  /** Grid-specific notes listed after the generated keyboard help (what a
   *  drag does, what the colors mean). Ignored when `helpContent` is set. */
  helpNotes?: React.ReactNode
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

  // -- Row activation (enabled when provided) --
  /**
   * Makes whole rows openable: click, or Enter/Space with the row focused.
   * Supplying it turns each body row into a focusable `role="button"`, so the
   * list is navigable by keyboard without a link in every primary cell.
   *
   * Clicks on interactive cell content (links, buttons, inputs, and anything
   * marked `data-row-activate="ignore"`) are left alone — an actions dropdown
   * inside an activatable row still just opens the dropdown.
   *
   * Flat mode only.
   */
  onRowActivate?: (row: T) => void
  /**
   * Background for the whole row. Painted on every body cell, including the
   * filler, so zebra, hover, and pinned cells do not cover it.
   */
  getRowBackground?: (row: T) => string | undefined
  /**
   * The activated row's id, matched against {@link getRowId}. Drives the
   * highlight; the grid does not track it, so the consumer owns the value and
   * can keep it in the URL.
   */
  activatedRowId?: string | null
  /**
   * Accessible name for an activatable row, e.g. `(row) => row.name`. Without
   * it the row's button role announces its whole flattened text.
   */
  getRowActivateLabel?: (row: T) => string

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

  // -- Row grouping (flat mode; turned on by providing grouping) --
  /**
   * Column ids to group the rows by, outermost first. Each distinct value of
   * the column becomes a collapsible header row spanning the grid, with the
   * matching rows underneath; the grouped column itself leaves the row cells,
   * since its value is now the heading. Sorting applies within each group and
   * orders the groups by the grouped column's own sort. Filters and quick
   * search apply to the rows, and a group with no match disappears.
   *
   * Flat mode only: a tree already has a hierarchy of its own. Turns off row
   * reorder, since the displayed order is no longer the data order.
   */
  grouping?: string[]
  /**
   * What a group's header row says. Defaults to the group value and a row
   * count; supply this to add what the rows underneath add up to.
   */
  renderGroupHeader?: (context: GroupHeaderContext<T>) => React.ReactNode
  /** Whether every group starts expanded. On by default. */
  initialGroupsExpanded?: boolean

  // -- Tree mode (turned on by providing getSubRows) --
  /** How to extract child rows. Presence of this prop enables tree mode. */
  getSubRows?: (row: T) => T[] | undefined
  /**
   * Whether every node starts expanded. On by default — a hierarchy is usually the point of the
   * grid. Off for a list whose parents are rollups of their children, where the children are
   * the detail and the parent row already says what the reader came for.
   */
  initialExpanded?: boolean

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

  // -- Cell editing (enabled by onCommit plus a column with meta.editor) --
  /**
   * Saves one edited cell — or a pair of linked cells — of an existing row.
   * The patch holds only the changed fields, keyed by each editor's `field`.
   * Reject (an RTK `.unwrap()` does) to mark the cells as failed: the user's
   * values stay on them, and a 422's errors are shown on the cells they name.
   * Commits for a row run one at a time.
   */
  onCommit?: (rowId: string, patch: Record<string, unknown>) => Promise<void>
  /**
   * Creates a draft row once its tree column (its Name) commits, with every
   * value the user put on the draft. Resolve with the new row's id so focus
   * can follow it. A draft added below a row (Insert) also gets its 1-based
   * `order` among its siblings; one added last gets none.
   */
  onCreate?: (
    parentId: string | null,
    values: Record<string, unknown>,
    position?: { order: number },
  ) => Promise<string | void>
  /**
   * Server error keys whose names match no editor's `field`, mapped to the
   * column that shows them (`{ assigneeIds: 'assignees' }`).
   */
  fieldToColumn?: Record<string, string>

  // -- Drafts (tree mode; enabled by createDraftNode with onCreate and onCommit) --
  /** Factory to create a full row node from a draft item. */
  createDraftNode?: (draft: DraftItem) => T
  /** Called when a draft is cancelled (e.g., Escape key on a draft row). */
  onDraftCancelled?: (draftId: string) => void

  // -- Row actions from the keyboard (tree mode) --
  /**
   * What the row-action keys do beyond the grid's own. Insert or Ctrl+Enter
   * adds a draft below the row when the grid creates drafts, and
   * Alt+Shift+→/← indent and outdent through `moveValidator` and
   * `onNodeMove`; these fill in the rest. The help panel lists each key the
   * grid supports.
   */
  rowActions?: WaydGridRowActions<T>

  // -- Right chart pane (Gantt "chart mode"; enabled when provided) --
  /**
   * Attach a bar-chart pane to the right of the grid that shares the grid's row
   * geometry (see {@link WaydGridRightPane}). Turns the list into a Gantt while
   * keeping every grid feature. Omit for a plain grid — the body structure is
   * completely unchanged when this is absent.
   */
  rightPane?: WaydGridRightPane<T>
}

/** The consumer's half of the row-action keys ({@link WaydGridProps.rowActions}). */
export interface WaydGridRowActions<T extends RowData> {
  /** Ctrl+Delete. Ask before deleting: the key is easy to press by accident.
   *  Return false for a row that cannot be deleted, and the key is left to
   *  the grid's other handlers. */
  onDelete?: (row: T) => boolean | void
  /** Ctrl+Shift+Enter: open the row's details. Return false for a row that
   *  has none. */
  onOpen?: (row: T) => boolean | void
  /** Whether Insert may add a row under this parent (null: the root).
   *  Every parent by default. */
  canInsertUnder?: (parent: T | null) => boolean
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
  /** The displayed (post filter + sort) rows' data, in display order. */
  getDisplayedRows: () => unknown[]
  /**
   * Commits values set outside a cell — a dragged Gantt bar — as a cell edit
   * would: the editors' and pairs' checks run, the cells show the values
   * marked saving, the commit waits behind the row's other commits, and a
   * failure lands on the cells. Values are keyed by column id, in the form
   * each editor's `get` returns. Needs cell editing (`onCommit`).
   */
  commitCells: (rowId: string, values: Record<string, unknown>) => void
  /**
   * Moves focus to a cell once its row is in the data — after a refetch that
   * removed the focused row, say — if the user is still in the grid and no
   * editor is open. Needs cell editing (`onCommit`).
   */
  focusRow: (rowId: string, columnId: string) => void
}
