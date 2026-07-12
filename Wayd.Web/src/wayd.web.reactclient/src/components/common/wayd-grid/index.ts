// Types
export type {
  FilterOption,
  WaydGridColumnMeta,
  WaydGridProps,
  WaydGridHandle,
  GridColumnContext,
  GridInlineEditingConfig,
  RowReorderEvent,
} from './types'

// Tree-mode types + utilities (re-exported from the shared grid core so
// consumers have a single import surface)
export type {
  TreeNode,
  FlattenedTreeNode,
  DragProjection,
  MoveValidator,
} from '../wayd-grid-core/dnd/tree-projection'
export { defaultMoveValidator } from '../wayd-grid-core/dnd/tree-projection'
export type { DraftItem } from '../wayd-grid-core/draft-utils'
export {
  buildTree,
  countTreeNodes,
  findNodeById,
  flattenTree,
} from '../wayd-grid-core/tree-utils'
export { useGridDragHandle } from '../wayd-grid-core/dnd/grid-dnd'

// Filter functions (re-exported from the shared grid core so wayd-grid
// consumers have a single import surface)
export {
  stringContainsFilter,
  setContainsFilter,
  numberRangeFilter,
} from '../wayd-grid-core/grid-filters'

// Sorting utilities
export {
  caseInsensitiveCompare,
  dateSortBy,
  sortEmptyLast,
  workItemKeySort,
  workStatusCategorySort,
} from '../wayd-grid-core/grid-sorting'

// Multi-value set filter (set panel over individual values, e.g. a Roles column
// whose accessor is the comma-joined names). Pair with meta.filterOptions.
export { createMultiValueSetFilter } from '../wayd-grid-core/filters'

// Multi-value ("CSV") column factory — builds a tag-list cell + multi-value set
// filter + derived filterOptions for a column whose data is comma-separated
// values or an array. Prefer this over wiring those three pieces by hand.
export { createCsvColumn, splitCsv } from '../wayd-grid-core/csv-column'
export type { CsvColumnOptions } from '../wayd-grid-core/csv-column'
export { default as TagListCell } from '../wayd-grid-core/tag-list-cell'
export type { TagListCellProps } from '../wayd-grid-core/tag-list-cell'

// Column types (declarative via meta.columnType) + helpers — moved to the
// shared grid core; re-exported so consumers keep a single import surface
export {
  applyColumnType,
  formatDateOnly,
  formatDateTime,
  YES,
  NO,
} from '../wayd-grid-core/column-types'
export type { WaydColumnType } from './types'

// Reusable row-actions column (⋯ dropdown, per-row getItems)
export {
  createActionsColumn,
  ACTIONS_COLUMN_SIZE,
} from '../wayd-grid-core/actions-column'
export type { ActionsColumnOptions } from '../wayd-grid-core/actions-column'

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
  renderWorkItemLink,
  renderAssignedToLink,
  renderWorkStatusTag,
  renderDependencyHealthTag,
} from '../wayd-grid-core/cell-renderers'
export type {
  TeamLinkTarget,
  NavLinkTarget,
  SprintLinkTarget,
  UserLinkTarget,
  WorkItemLinkTarget,
  AssignedToLinkTarget,
  WorkStatusTagTarget,
  DependencyHealthTarget,
} from '../wayd-grid-core/cell-renderers'

// Column layout persistence (opt-in via WaydGrid's persistStateKey prop) —
// the account-page controls live outside the grid, hence the re-export
export {
  GRID_PERSISTENCE_ENABLED_KEY,
  clearAllGridColumnState,
} from '../wayd-grid-core/use-grid-persistence'

// Components (toolbar reconciled into the shared grid core; aliases kept)
export { default as WaydGridToolbar } from '../wayd-grid-core/grid-toolbar'
export type { GridToolbarProps as WaydGridToolbarProps } from '../wayd-grid-core/grid-toolbar'
export { default as WaydGrid } from './wayd-grid'
