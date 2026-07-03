import type { TreeNode } from '@/src/components/common/wayd-grid2'
import type { WorkItemListDto } from '@/src/services/wayd-api'

/**
 * Extends WorkItemListDto to satisfy the TreeNode interface required by the
 * grid's tree mode.
 */
export interface WorkItemTreeNode extends TreeNode, WorkItemListDto {
  children: WorkItemTreeNode[]
  parentId: string | null
}

/**
 * Converts a flat array of WorkItemListDto into a tree structure based on parent relationships.
 * Items whose parent is not in the list are treated as root nodes.
 */
export function buildWorkItemTree(
  items: WorkItemListDto[],
): WorkItemTreeNode[] {
  const idSet = new Set(items.map((item) => item.id))

  // Create tree nodes with empty children
  const nodeMap = new Map<string, WorkItemTreeNode>()
  for (const item of items) {
    const parentId = item.parent?.id ?? null
    nodeMap.set(item.id, {
      ...item,
      children: [],
      parentId: parentId && idSet.has(parentId) ? parentId : null,
    })
  }

  // Build tree by attaching children to parents
  const roots: WorkItemTreeNode[] = []
  for (const node of nodeMap.values()) {
    if (node.parentId && nodeMap.has(node.parentId)) {
      nodeMap.get(node.parentId)!.children.push(node)
    } else {
      roots.push(node)
    }
  }

  return roots
}
