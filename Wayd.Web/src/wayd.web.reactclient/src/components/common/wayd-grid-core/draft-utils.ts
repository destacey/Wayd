import type { TreeNode } from './dnd/tree-projection'

/**
 * Draft item for inline creation.
 */
export interface DraftItem {
  id: string
  parentId?: string
  order: number
}

/**
 * Merges in-memory draft rows into the existing tree for inline creation.
 * The `createDraftNode` factory function creates a domain-specific node from a DraftItem.
 *
 * @param items - The current tree data
 * @param drafts - Draft items to merge in
 * @param createDraftNode - Factory that creates a full tree node from a draft
 * @returns New tree with drafts inserted at the appropriate levels
 */
export function mergeDraftsIntoTree<T extends TreeNode>(
  items: T[],
  drafts: DraftItem[],
  createDraftNode: (draft: DraftItem) => T,
): T[] {
  if (drafts.length === 0) return items

  const insertDraftsAtLevel = (
    levelItems: T[],
    parentId: string | undefined,
  ): T[] => {
    const processedItems = levelItems.map((item) => {
      const currentChildren = (item.children ?? []) as T[]
      const nextChildren = insertDraftsAtLevel(currentChildren, item.id)

      if (
        nextChildren.length === currentChildren.length &&
        nextChildren.every((child, index) => child === currentChildren[index])
      ) {
        return item
      }

      return {
        ...item,
        children: nextChildren,
      }
    })

    const draftsForLevel = drafts.filter((d) => d.parentId === parentId)
    if (draftsForLevel.length > 0) {
      return [
        ...processedItems,
        ...draftsForLevel.map((draft) => createDraftNode(draft)),
      ]
    }

    return processedItems
  }

  return insertDraftsAtLevel(items, undefined)
}
