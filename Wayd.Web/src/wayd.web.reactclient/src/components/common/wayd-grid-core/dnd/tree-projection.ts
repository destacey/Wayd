// Tree-ONLY reparenting projection for grid drag-and-drop: horizontal offset →
// indentation depth → parent reassignment, with circular-reference prevention.
// The shared drag mechanics (sensors, sortable row) live in ./grid-dnd.

/**
 * Minimum shape that any tree node must satisfy.
 * Consumers extend this for their domain types.
 */
export interface TreeNode {
  id: string
  children: TreeNode[]
  parentId?: string | null
}

/**
 * Flattened tree node with DnD metadata. Generic over the node type.
 * Wraps the original node as `node: T` to avoid type conflicts when spreading.
 */
export interface FlattenedTreeNode<T extends TreeNode = TreeNode> {
  node: T
  depth: number
  parentId: string | null
  ancestorIds: string[]
  flatIndex: number
}

/**
 * Result of projecting where a dragged item will land.
 */
export interface DragProjection<T extends TreeNode = TreeNode> {
  depth: number
  maxDepth: number
  minDepth: number
  parentId: string | null
  parentNode: T | null
  canDrop: boolean
  reason?: string
}

/**
 * Validation function for domain-specific move rules.
 * The generic layer calls this; consumers supply domain logic.
 */
export type MoveValidator<T extends TreeNode = TreeNode> = (
  activeNode: FlattenedTreeNode<T>,
  targetParentNode: T | null,
  targetParentId: string | null,
) => { canMove: boolean; reason?: string }

/** Pixels of horizontal drag per indentation depth level. */
export const INDENTATION_WIDTH = 32

/**
 * Default structural move validator.
 * Checks for circular references and self-assignment only.
 * Domain-specific rules (e.g., "milestones can't have children") should be
 * provided as a custom MoveValidator.
 */
export function defaultMoveValidator<T extends TreeNode>(
  activeNode: FlattenedTreeNode<T>,
  targetParentNode: T | FlattenedTreeNode<T> | null,
  targetParentId: string | null,
): { canMove: boolean; reason?: string } {
  // No parent (root level) is always valid
  if (!targetParentId) {
    return { canMove: true }
  }

  // Cannot be its own parent
  if (activeNode.node.id === targetParentId) {
    return {
      canMove: false,
      reason: 'A node cannot be its own parent',
    }
  }

  // Check if target parent is a descendant of the active node
  // This prevents circular references
  if (targetParentNode && 'ancestorIds' in targetParentNode) {
    const ancestorIds = (targetParentNode as FlattenedTreeNode<T>).ancestorIds
    if (ancestorIds && ancestorIds.includes(activeNode.node.id)) {
      return {
        canMove: false,
        reason:
          'Cannot move a parent node into its own children or descendants',
      }
    }
  }

  return { canMove: true }
}

/**
 * Finds a node by ID in a flattened array.
 */
function findFlatNodeById<T extends TreeNode>(
  flatItems: FlattenedTreeNode<T>[],
  id: string,
): FlattenedTreeNode<T> | null {
  return flatItems.find((item) => item.node.id === id) ?? null
}

/**
 * Calculates the projected position and parent for a dragged node.
 * This is the core function that determines where a node will land based on:
 * - The vertical position (which node it's over)
 * - The horizontal offset (how far right/left it's been dragged)
 *
 * @param items - Flattened node array
 * @param activeId - ID of the node being dragged
 * @param overId - ID of the node being hovered over
 * @param dragOffset - Horizontal drag offset in pixels
 * @param indentationWidth - Pixels per indentation level
 * @param validator - Optional custom move validator (defaults to defaultMoveValidator)
 */
export function getProjection<T extends TreeNode>(
  items: FlattenedTreeNode<T>[],
  activeId: string,
  overId: string,
  dragOffset: number,
  indentationWidth: number = INDENTATION_WIDTH,
  validator?: MoveValidator<T>,
): DragProjection<T> {
  const activeNode = findFlatNodeById(items, activeId)
  const overNode = findFlatNodeById(items, overId)

  if (!activeNode || !overNode) {
    return {
      depth: 0,
      maxDepth: 0,
      minDepth: 0,
      parentId: null,
      parentNode: null,
      canDrop: false,
      reason: 'Node not found',
    }
  }

  const overIndex = items.findIndex((item) => item.node.id === overId)

  // Calculate depth change from horizontal drag offset
  const dragDepth = Math.round(dragOffset / indentationWidth)

  // When no horizontal drag (dragDepth === 0):
  // Default to matching the overNode's depth, but cap to not exceed
  // the activeNode's current depth unless user explicitly drags right
  let defaultDepth = overNode.depth
  if (dragDepth === 0 && overNode.depth > activeNode.depth) {
    defaultDepth = activeNode.depth
  }
  const projectedDepth =
    dragDepth === 0 ? defaultDepth : activeNode.depth + dragDepth

  // Use the "over" item's context to determine depth constraints
  const previousItem = overIndex > 0 ? items[overIndex - 1] : null

  // Special case: when dragging over self with positive offset, use previous item's depth
  const referenceNode =
    activeId === overId && dragDepth > 0 && previousItem
      ? previousItem
      : overNode
  const maxDepth = referenceNode.depth + 1
  const minDepth = 0

  // Constrain projected depth to valid range
  let depth = projectedDepth
  if (projectedDepth > maxDepth) {
    depth = maxDepth
  } else if (projectedDepth < minDepth) {
    depth = minDepth
  }

  // Calculate new parent ID based on depth
  const getParentId = (): string | null => {
    if (depth === 0) {
      return null // Root level
    }

    // If same depth as over node, they share the same parent
    if (depth === overNode.depth) {
      return overNode.parentId
    }

    // If deeper than over node, find parent by looking backwards
    if (depth > overNode.depth) {
      for (let i = overIndex - 1; i >= 0; i--) {
        const item = items[i]
        if (item.node.id === activeId) continue
        if (item.depth === depth - 1) {
          return item.node.id
        }
        if (item.depth < depth - 1) {
          break
        }
      }
      return null
    }

    // If shallower than over node, find appropriate parent
    for (let i = overIndex; i >= 0; i--) {
      const item = items[i]
      if (item.node.id === activeId) continue
      if (item.depth === depth - 1) {
        return item.node.id
      }
      if (item.depth < depth - 1) {
        break
      }
    }

    return null
  }

  const parentId = getParentId()

  // Find the parent node for validation
  let parentNode: T | null = null
  let parentFlat: FlattenedTreeNode<T> | null = null
  if (parentId) {
    parentFlat = findFlatNodeById(items, parentId)
    if (parentFlat) {
      parentNode = parentFlat.node
    }
  }

  // Run structural validation (needs flattened node for ancestor check)
  const structuralResult = defaultMoveValidator(activeNode, parentFlat, parentId)
  // Run domain validation (consumer validator receives the actual T node)
  const validate = validator ?? defaultMoveValidator
  const validation = validator
    ? structuralResult.canMove
      ? validate(activeNode, parentNode, parentId)
      : structuralResult
    : structuralResult

  return {
    depth,
    maxDepth,
    minDepth,
    parentId,
    parentNode,
    canDrop: validation.canMove,
    reason: validation.reason,
  }
}

/**
 * Calculates the new order/position for a node within its parent.
 * Returns a 1-based index (the API uses 1-based ordering).
 */
export function calculateOrderInParent<T extends TreeNode>(
  flatItems: FlattenedTreeNode<T>[],
  activeId: string,
  overIndex: number,
  parentId: string | null,
): number {
  // Get all current siblings with the same parent (excluding the active node)
  const siblings = flatItems.filter(
    (item) => item.parentId === parentId && item.node.id !== activeId,
  )

  if (siblings.length === 0) {
    return 1 // First (and only) item in parent
  }

  // Find the active node's original position to determine drag direction
  const activeIndex = flatItems.findIndex((item) => item.node.id === activeId)
  const isDraggingDown = activeIndex < overIndex

  // Find the node we're hovering over
  const overNode = flatItems[overIndex]

  // If we're hovering over a node with the same parent
  if (overNode.parentId === parentId) {
    const overSiblingIndex = siblings.findIndex(
      (s) => s.node.id === overNode.node.id,
    )
    if (overSiblingIndex >= 0) {
      return isDraggingDown ? overSiblingIndex + 2 : overSiblingIndex + 1
    }
  }

  // Find the first sibling that comes after overIndex in the flat array
  for (let i = 0; i < siblings.length; i++) {
    const siblingFlatIndex = flatItems.findIndex(
      (t) => t.node.id === siblings[i].node.id,
    )
    if (siblingFlatIndex > overIndex) {
      return i + 1 // 1-based index
    }
  }

  // If no sibling comes after overIndex, insert at the end
  return siblings.length + 1
}

/**
 * Updates a node's parent and order in the flattened array.
 * Creates a new array with the updated node metadata.
 */
export function updateNodePlacement<T extends TreeNode>(
  flatItems: FlattenedTreeNode<T>[],
  nodeId: string,
  newParentId: string | null,
  newDepth: number,
): FlattenedTreeNode<T>[] {
  return flatItems.map((item) => {
    if (item.node.id === nodeId) {
      let newAncestorIds: string[] = []
      if (newParentId) {
        const parent = flatItems.find((f) => f.node.id === newParentId)
        newAncestorIds = parent
          ? [...parent.ancestorIds, newParentId]
          : [newParentId]
      }

      return {
        ...item,
        parentId: newParentId,
        depth: newDepth,
        ancestorIds: newAncestorIds,
      }
    }
    return item
  })
}
