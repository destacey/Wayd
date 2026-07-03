import type { TreeNode, FlattenedTreeNode } from './dnd/tree-projection'

/**
 * Recursively counts all nodes in a tree (including children at all levels).
 */
export function countTreeNodes<T extends TreeNode>(nodes: T[]): number {
  return nodes.reduce(
    (acc, node) =>
      acc + 1 + (node.children?.length ? countTreeNodes(node.children as T[]) : 0),
    0,
  )
}

/**
 * Recursively finds a node by ID in a tree structure.
 * Returns null if not found.
 */
export function findNodeById<T extends TreeNode>(
  nodes: T[],
  id: string,
): T | null {
  for (const node of nodes) {
    if (node.id === id) return node
    if (node.children?.length) {
      const found = findNodeById(node.children as T[], id)
      if (found) return found as T
    }
  }
  return null
}

/**
 * Flattens a tree structure into an array with depth and parent metadata.
 * Used to work with dnd-kit's flat array model while preserving hierarchical relationships.
 */
export function flattenTree<T extends TreeNode>(
  items: T[],
  parentId: string | null = null,
  depth: number = 0,
  ancestorIds: string[] = [],
): FlattenedTreeNode<T>[] {
  const result: FlattenedTreeNode<T>[] = []
  let flatIndex = 0

  const flatten = (
    nodes: T[],
    currentParentId: string | null,
    currentDepth: number,
    currentAncestors: string[],
  ): void => {
    nodes.forEach((node) => {
      result.push({
        node,
        depth: currentDepth,
        parentId: currentParentId,
        ancestorIds: currentAncestors,
        flatIndex: flatIndex++,
      })

      if (node.children && node.children.length > 0) {
        flatten(node.children as T[], node.id, currentDepth + 1, [
          ...currentAncestors,
          node.id,
        ])
      }
    })
  }

  flatten(items, parentId, depth, ancestorIds)
  return result
}

/**
 * Rebuilds a tree structure from a flattened array.
 * Used after drag-and-drop to reconstruct the hierarchical structure.
 */
export function buildTree<T extends TreeNode>(
  flatItems: FlattenedTreeNode<T>[],
): T[] {
  const ROOT_ID = '__root__'

  // Create lookup map for all nodes, initialized with empty children
  const nodes: Record<string, T> = {}
  const rootChildren: T[] = []

  // Initialize all items with empty children arrays
  const items = flatItems.map((item) => {
    const clone = {
      ...item.node,
      children: [] as TreeNode[],
    } as T
    nodes[clone.id] = clone
    return { clone, parentId: item.parentId }
  })

  // Attach children to parents
  for (const { clone, parentId } of items) {
    if (parentId && nodes[parentId]) {
      nodes[parentId].children.push(clone)
    } else {
      rootChildren.push(clone)
    }
  }

  return rootChildren
}
