import type { ColumnDef } from '@tanstack/react-table'
import type { FormInstance } from 'antd'
import type {
  MoveValidator,
  TreeNode,
} from '../wayd-grid-core/dnd/tree-projection'
import type { GridEditingConfig } from '../wayd-grid-core/use-grid-editing'

// Tree node + DnD types moved to the shared grid core; re-exported so
// tree-grid consumers keep a single import surface.
export type {
  TreeNode,
  FlattenedTreeNode,
  DragProjection,
  MoveValidator,
} from '../wayd-grid-core/dnd/tree-projection'

// Editing types moved to the shared grid core; re-exported likewise.
export type { RowClickArgs } from '../wayd-grid-core/use-grid-editing'

/**
 * Configuration for the inline editing hook.
 * Alias of the shared grid-core config, kept under the tree-grid name.
 */
export type TreeGridEditingConfig<T extends TreeNode> = GridEditingConfig<T>

/**
 * Draft item for inline creation.
 */
export interface DraftItem {
  id: string
  parentId?: string
  order: number
}

/**
 * Filter option for select-type column filters.
 */
export interface FilterOption {
  label: string
  value: string
}

/**
 * Props for the generic TreeGridToolbar.
 */
export interface TreeGridToolbarProps {
  displayedRowCount: number
  totalRowCount: number
  searchValue: string
  onSearchChange: (e: React.ChangeEvent<HTMLInputElement>) => void
  onRefresh?: () => Promise<any>
  onClearFilters: () => void
  hasActiveFilters: boolean
  onExportCsv?: () => void
  isLoading: boolean
  /** Slot for domain-specific actions (e.g., create button) rendered on the left. */
  leftSlot?: React.ReactNode
  /** Content rendered inside the help popover. */
  helpContent?: React.ReactNode
  /** Slot for actions rendered on the far right of the toolbar (e.g., view selector). */
  rightSlot?: React.ReactNode
}

/**
 * Extended column metadata for TreeGrid.
 * Stored in TanStack's `columnDef.meta` field.
 */
export interface TreeGridColumnMeta {
  /** Filter UI type. Omit to use text input (the default when column has filtering enabled). */
  filterType?: 'text' | 'select' | 'numericRange'
  /** Options for 'select' filter type. */
  filterOptions?: FilterOption[]
  /** Placeholder text for text/numericRange filter inputs. */
  filterPlaceholder?: string
  /** Whether to include this column in CSV export. Default: true if column has an accessor. */
  enableExport?: boolean
  /** Custom CSV formatter for this column's values. */
  exportFormatter?: (value: unknown, row: any) => string
  /** Override the CSV header text for this column. */
  exportHeader?: string
}

/**
 * Context passed to the `columns` and `leftSlot` function props of TreeGrid.
 * Provides editing, DnD, and draft state so domain code can build columns reactively.
 */
export interface TreeGridColumnContext {
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
 * Inline editing configuration for TreeGrid consumers.
 * TreeGrid fills in `data`, `tableWrapperClassName`, `fieldErrors`, `setFieldErrors`,
 * and `onCancelDraft` internally.
 */
export interface TreeGridInlineEditingConfig<T extends TreeNode> {
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
 * Props for the TreeGrid component.
 */
export interface TreeGridProps<T extends TreeNode> {
  /** Tree data (without drafts — TreeGrid merges drafts internally). */
  data: T[]
  /** How to extract child rows. Default: `(row) => row.children as T[]` */
  getSubRows?: (row: T) => T[]
  /** Loading state. */
  isLoading: boolean
  /**
   * Column definitions. Can be a static array or a function that receives
   * editing/DnD/draft context and returns columns.
   */
  columns:
    | ColumnDef<T, any>[]
    | ((context: TreeGridColumnContext) => ColumnDef<T, any>[])

  // -- Toolbar --
  onRefresh?: () => Promise<any>
  /**
   * Left slot for the toolbar. Can be a ReactNode or a function receiving context
   * (useful for Create buttons that need `canCreateDraft` / `addDraftAtRoot`).
   */
  leftSlot?:
    | React.ReactNode
    | ((context: TreeGridColumnContext) => React.ReactNode)
  helpContent?: React.ReactNode
  /** Slot for actions rendered on the far right of the toolbar (e.g., view selector). */
  rightSlot?: React.ReactNode
  emptyMessage?: string
  /** Fixed height in pixels. When omitted, the grid auto-sizes to fill the remaining viewport height. */
  height?: number
  /** File name prefix for CSV export (e.g., 'project-tasks'). */
  csvFileName?: string

  // -- DnD (enabled when onNodeMove is provided) --
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
  moveValidator?: MoveValidator<T>

  // -- Inline editing (enabled when editingConfig is provided) --
  editingConfig?: TreeGridInlineEditingConfig<T>
  /** External field-level validation errors (e.g., from API 422 responses). */
  fieldErrors?: Record<string, string>
  /** Called when field errors change (cleared on successful validation, set on failure). */
  onFieldErrorsChange?: (errors: Record<string, string>) => void

  // -- Drafts (enabled when createDraftNode is provided) --
  /** Factory to create a full tree node from a draft item. */
  createDraftNode?: (draft: DraftItem) => T
  /** Called when a draft is cancelled (e.g., Escape key on a draft row). */
  onDraftCancelled?: (draftId: string) => void
  /** Called when the internal draft list changes. */
  onDraftsChange?: (drafts: DraftItem[]) => void
}

/**
 * Handle exposed by TreeGrid via ref.
 */
export interface TreeGridHandle {
  /** The TanStack table instance. */
  table: any
  /** The currently selected row ID (from editing hook), or null. */
  selectedRowId: string | null
}
