// Types
export type {
  TreeNode,
  FlattenedTreeNode,
  DragProjection,
  MoveValidator,
  DraftItem,
  FilterOption,
  RowClickArgs,
  TreeGridEditingConfig,
  TreeGridToolbarProps,
  TreeGridColumnMeta,
  TreeGridColumnContext,
  TreeGridInlineEditingConfig,
  TreeGridProps,
  TreeGridHandle,
} from './types'

// Tree utilities
export { countTreeNodes, findNodeById, flattenTree, buildTree } from './tree-utils'

// Draft utilities
export { mergeDraftsIntoTree } from './draft-utils'

// DnD utilities (moved to the shared grid core; re-exported so existing
// tree-grid consumers keep a single import surface)
export {
  INDENTATION_WIDTH,
  defaultMoveValidator,
  getProjection,
  calculateOrderInParent,
  updateNodePlacement,
} from '../wayd-grid-core/dnd/tree-projection'
export { DRAG_ACTIVATION_DISTANCE } from '../wayd-grid-core/dnd/grid-dnd'

// Filter functions (moved to the shared grid core; re-exported so existing
// tree-grid consumers keep a single import surface)
export {
  stringContainsFilter,
  setContainsFilter,
  numberRangeFilter,
} from '../wayd-grid-core/grid-filters'

// Sorting utilities
export { dateSortBy } from '../wayd-grid-core/grid-sorting'

// React components (sortable row moved to the shared grid core; the tree-grid
// names are aliases kept for existing consumers)
export {
  GridSortableRow as TreeGridSortableRow,
  useGridDragHandle as useTreeGridDragHandle,
} from '../wayd-grid-core/dnd/grid-dnd'
export { default as TreeGridToolbar } from './tree-grid-toolbar'

// Hooks (editing hook moved to the shared grid core; alias kept)
export { useGridEditing as useTreeGridEditing } from '../wayd-grid-core/use-grid-editing'

// TreeGrid component
export { default as TreeGrid } from './tree-grid'
