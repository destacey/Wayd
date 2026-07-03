// Types
export type {
  FilterOption,
  WaydGridColumnMeta,
  WaydGrid2Props,
  WaydGrid2Handle,
  GridColumnContext,
  GridInlineEditingConfig,
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

// Filter functions (re-exported from the shared grid core so wayd-grid2
// consumers have a single import surface)
export {
  stringContainsFilter,
  setContainsFilter,
  numberRangeFilter,
} from '../wayd-grid-core/grid-filters'

// Sorting utilities
export { dateSortBy, sortEmptyLast } from '../wayd-grid-core/grid-sorting'

// Column types (declarative via meta.columnType) + helpers — moved to the
// shared grid core; re-exported so consumers keep a single import surface
export { applyColumnType, YES, NO } from '../wayd-grid-core/column-types'
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
} from '../wayd-grid-core/cell-renderers'
export type {
  TeamLinkTarget,
  NavLinkTarget,
  SprintLinkTarget,
  UserLinkTarget,
} from '../wayd-grid-core/cell-renderers'

// Components (toolbar reconciled into the shared grid core; aliases kept)
export { default as WaydGrid2Toolbar } from '../wayd-grid-core/grid-toolbar'
export type { GridToolbarProps as WaydGrid2ToolbarProps } from '../wayd-grid-core/grid-toolbar'
export { default as WaydGrid2 } from './wayd-grid2'
