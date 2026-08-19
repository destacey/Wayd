// Shared grid engine powering the unified WaydGrid (components/common/wayd-grid).

import type {
  Cell as CellCore,
  CellContext as CellContextCore,
  Column as ColumnCore,
  ColumnDef as ColumnDefCore,
  FilterFn as FilterFnCore,
  Header as HeaderCore,
  HeaderContext as HeaderContextCore,
  HeaderGroup as HeaderGroupCore,
  Row as RowCore,
  RowData as RowDataCore,
  SortFn as SortFnCore,
  TableOptions as TableOptionsCore,
  TableState as TableStateCore,
} from '@tanstack/table-core'
import type { ReactTable } from '@tanstack/react-table'

import type { WaydGridFeatures } from './grid-features'

// TanStack table types with the grid's feature set pre-bound.
//
// v9 puts a TFeatures generic first on every public type (ColumnDef<TFeatures,
// TData, TValue>), so importing these straight from '@tanstack/react-table'
// binds the ROW type to the features slot and fails to compile. Binding it
// once here keeps call sites at the familiar (TData, TValue) arity and makes
// this the only place that knows which features the grid runs on. Import
// table types from this barrel, never from '@tanstack/react-table' directly.
export type Cell<TData extends RowDataCore, TValue> = CellCore<
  WaydGridFeatures,
  TData,
  TValue
>
export type Column<TData extends RowDataCore, TValue> = ColumnCore<
  WaydGridFeatures,
  TData,
  TValue
>
export type ColumnDef<TData extends RowDataCore, TValue = unknown> =
  ColumnDefCore<WaydGridFeatures, TData, TValue>
export type Header<TData extends RowDataCore, TValue> = HeaderCore<
  WaydGridFeatures,
  TData,
  TValue
>
export type HeaderGroup<TData extends RowDataCore> = HeaderGroupCore<
  WaydGridFeatures,
  TData
>
export type Row<TData extends RowDataCore> = RowCore<WaydGridFeatures, TData>
export type Table<TData extends RowDataCore> = ReactTable<
  WaydGridFeatures,
  TData
>
export type TableOptions<TData extends RowDataCore> = TableOptionsCore<
  WaydGridFeatures,
  TData
>
export type {
  ColumnFiltersState,
  ColumnOrderState,
  ColumnPinningState,
  ColumnSizingState,
  ColumnPinningPosition,
  RowData,
  SortingState,
} from '@tanstack/react-table'
export type { WaydGridFeatures } from './grid-features'
export { waydGridFeatures } from './grid-features'

export type TableState = TableStateCore<WaydGridFeatures>
export type FilterFn<TData extends RowDataCore> = FilterFnCore<
  WaydGridFeatures,
  TData
>
export type SortingFn<TData extends RowDataCore> = SortFnCore<
  WaydGridFeatures,
  TData
>
export type CellContext<TData extends RowDataCore, TValue> = CellContextCore<
  WaydGridFeatures,
  TData,
  TValue
>
export type HeaderContext<TData extends RowDataCore, TValue> = HeaderContextCore<
  WaydGridFeatures,
  TData,
  TValue
>
export type { ColumnVisibilityState as VisibilityState } from '@tanstack/react-table'
export { flexRender } from '@tanstack/react-table'

// Shared column meta types (+ TanStack ColumnMeta module augmentation)
export type {
  FilterOption,
  WaydColumnType,
  WaydGridColumnMeta,
} from './types'

// Filter functions
export {
  stringContainsFilter,
  setContainsFilter,
  numberRangeFilter,
} from './grid-filters'

// Descriptor filter engine + filter UI (popup, floating row, set/date panels)
export * from './filters'

// Column types (declarative via meta.columnType) + helpers
export { applyColumnType, YES, NO, YES_NO_COLUMN_SIZE } from './column-types'

// Reusable row-actions column (⋯ dropdown, per-row getItems)
export { createActionsColumn, ACTIONS_COLUMN_SIZE } from './actions-column'
export type { ActionsColumnOptions } from './actions-column'

// Cell renderers (link builders taking the domain object)
export {
  renderTeamLink,
  renderPlanningIntervalLink,
  renderProjectLink,
  renderPortfolioLink,
  renderProgramLink,
  renderWorkspaceLink,
  renderSprintLink,
  renderUserLink,
  renderDependencyHealthTag,
} from './cell-renderers'
export type {
  TeamLinkTarget,
  NavLinkTarget,
  SprintLinkTarget,
  UserLinkTarget,
  DependencyHealthTarget,
} from './cell-renderers'

// Sorting utilities
export { dateSortBy, sortEmptyLast } from './grid-sorting'

// CSV export
export { exportGridToCsv } from './grid-export'

// Table config + shared state hooks
export {
  mergeColumnVisibility,
  useGridState,
  useGridTable,
} from './use-grid-table'
export type {
  GridState,
  UseGridStateOptions,
  UseGridTableOptions,
} from './use-grid-table'

// Column layout persistence (opt-in via WaydGrid's persistStateKey prop)
export {
  GRID_PERSISTENCE_ENABLED_KEY,
  GRID_STATE_KEY_PREFIX,
  GRID_STATE_VERSION,
  clearAllGridColumnState,
  gridStateStorageKey,
  isGridPersistenceEnabled,
  isPersistedColumnState,
  useGridColumnStatePersistence,
} from './use-grid-persistence'
export type { PersistedColumnState } from './use-grid-persistence'

// Column pinning (sticky rendering over TanStack's columnPinning state)
export {
  getPinnedBandOffsets,
  getPinnedOffsets,
  pinnedCellClassNames,
  pinnedCellStyle,
} from './column-pinning'
export type {
  PinnedCellClasses,
  PinnedColumnOffsets,
} from './column-pinning'

// Column autosize (measure rendered content, apply via columnSizing)
export {
  AUTOSIZE_MAX_WIDTH,
  AUTOSIZE_MIN_WIDTH,
  computeAutosizeWidth,
  measureColumnContent,
} from './column-autosize'
export type {
  AutosizeWidthInput,
  ColumnContentMeasurement,
} from './column-autosize'

// Per-column header menu (⋮ — sort, pin, autosize, choose columns, reset)
export {
  ColumnChooserModal,
  ColumnMenuTrigger,
  buildColumnMenuItems,
  getColumnChooserOptions,
} from './column-menu'
export type {
  ColumnChooserModalProps,
  ColumnChooserOption,
  ColumnMenuItemsInput,
  ColumnMenuTriggerProps,
} from './column-menu'

// Toolbar (search, row count, refresh, clear, export, help)
export { default as GridToolbar } from './grid-toolbar'
export type { GridToolbarProps } from './grid-toolbar'

// Row renderer — the flat and tree forms of the row-renderer seam
export { FlatGridRow, SortableFlatGridRow, TreeGridRow } from './grid-row'
export type {
  FlatGridRowProps,
  GridRowClasses,
  SortableFlatGridRowProps,
  TreeGridRowClasses,
  TreeGridRowProps,
} from './grid-row'

// Tree + draft utilities (tree mode)
export {
  buildTree,
  countTreeNodes,
  findNodeById,
  flattenTree,
} from './tree-utils'
export { mergeDraftsIntoTree } from './draft-utils'
export type { DraftItem } from './draft-utils'

// Header sort/resize cell
export {
  GridHeaderCell,
  GridHeaderContent,
  useResizeClickGuard,
} from './grid-header-row'
export type {
  GridHeaderCellClasses,
  GridHeaderCellProps,
  ResizeClickGuard,
} from './grid-header-row'

// Inline editing hook (grid-agnostic; rows only need an id)
export { useGridEditing } from './use-grid-editing'
export type { GridEditingConfig, RowClickArgs } from './use-grid-editing'

// DnD — shared mechanics
export {
  DRAG_ACTIVATION_DISTANCE,
  GridSortableRow,
  useGridDndSensors,
  useGridDragHandle,
} from './dnd/grid-dnd'

// DnD — tree-only reparenting projection
export {
  INDENTATION_WIDTH,
  calculateOrderInParent,
  defaultMoveValidator,
  getProjection,
  updateNodePlacement,
} from './dnd/tree-projection'
export type {
  DragProjection,
  FlattenedTreeNode,
  MoveValidator,
  TreeNode,
} from './dnd/tree-projection'
