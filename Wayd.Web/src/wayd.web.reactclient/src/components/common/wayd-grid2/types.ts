import type { ColumnDef } from '@tanstack/react-table'
import type { FormInstance } from 'antd'
import type { DraftItem } from '../wayd-grid-core/draft-utils'
import type { MoveValidator } from '../wayd-grid-core/dnd/tree-projection'

// Column meta + filter option types moved to the shared grid core (including
// the TanStack ColumnMeta module augmentation); re-exported so wayd-grid2
// consumers keep a single import surface.
export type {
  FilterOption,
  WaydColumnType,
  WaydGridColumnMeta,
} from '../wayd-grid-core/types'

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
 * Inline editing configuration for WaydGrid2 consumers.
 * The grid fills in `data`, `tableWrapperClassName`, `fieldErrors`,
 * `setFieldErrors`, and `onCancelDraft` internally.
 */
export interface GridInlineEditingConfig<T> {
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
 * Props for the WaydGrid2 component — flat by default; provide `getSubRows`
 * to turn on tree mode (expansion, indentation via caller columns,
 * filterFromLeafRows, and — when configured — reparenting DnD, inline
 * editing, and draft rows).
 */
export interface WaydGrid2Props<T> {
  /** Row data. In tree mode, the root nodes (children come from getSubRows). */
  data: T[]
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
}

/**
 * Handle exposed by WaydGrid2 via ref.
 */
export interface WaydGrid2Handle {
  /** The underlying TanStack table instance. */
  table: any
  /** The currently selected row ID (from the editing hook), or null. */
  selectedRowId: string | null
}
