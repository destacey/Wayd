// Shared grid engine for WaydGrid and TreeGrid — converging into the unified
// WaydGrid (see docs/contributing/specs/grid-core-implementation-plan.md).

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
export { useGridState, useGridTable } from './use-grid-table'
export type {
  GridState,
  UseGridStateOptions,
  UseGridTableOptions,
} from './use-grid-table'

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
