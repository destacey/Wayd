// Shared grid engine powering the unified WaydGrid (components/common/wayd-grid).

import type { LegacyFeatures as LegacyFeaturesType } from '@tanstack/react-table/legacy'
import type {
  CellContext as CellContextCore,
  FilterFn as FilterFnCore,
  HeaderContext as HeaderContextCore,
  RowData as RowDataCore,
  SortFn as SortFnCore,
  TableState as TableStateCore,
} from '@tanstack/table-core'

// TanStack table types, re-exported under their v8 names.
//
// v9 puts a TFeatures generic first on every public type (ColumnDef<TFeatures,
// TData, TValue>), so importing these straight from '@tanstack/react-table'
// silently binds the row type to the FEATURES slot and fails to compile. The
// grid runs on the v9 `/legacy` shim, whose Legacy* aliases keep the v8 arity
// (TData, TValue). Import table types from this barrel, never from
// '@tanstack/react-table' directly, so the whole app moves off legacy in one
// place when the core migrates to native v9 features.
export type {
  LegacyCell as Cell,
  LegacyColumn as Column,
  LegacyColumnDef as ColumnDef,
  LegacyHeader as Header,
  LegacyHeaderGroup as HeaderGroup,
  LegacyRow as Row,
  LegacyReactTable as Table,
  LegacyTableOptions as TableOptions,
} from '@tanstack/react-table/legacy'
export type { LegacyFeatures } from '@tanstack/react-table/legacy'
export type {
  ColumnFiltersState,
  ColumnOrderState,
  ColumnPinningState,
  ColumnSizingState,
  ColumnPinningPosition,
  RowData,
  SortingState,
} from '@tanstack/react-table'

// These v9 types take TFeatures first; bind it to the legacy feature set so
// call sites keep the v8 arity.
export type TableState = TableStateCore<LegacyFeaturesType>
export type FilterFn<TData extends RowDataCore> = FilterFnCore<
  LegacyFeaturesType,
  TData
>
export type SortingFn<TData extends RowDataCore> = SortFnCore<
  LegacyFeaturesType,
  TData
>
export type CellContext<TData extends RowDataCore, TValue> = CellContextCore<
  LegacyFeaturesType,
  TData,
  TValue
>
export type HeaderContext<TData extends RowDataCore, TValue> = HeaderContextCore<
  LegacyFeaturesType,
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
