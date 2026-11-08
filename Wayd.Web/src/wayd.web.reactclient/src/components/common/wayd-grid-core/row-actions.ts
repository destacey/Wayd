import type { FlattenedTreeNode, TreeNode } from './dnd/tree-projection'

/** A row action the keyboard can ask for (see the keymap's Rows section). */
export type RowActionKey = 'insert' | 'indent' | 'outdent' | 'delete' | 'open'

/**
 * The row action a key press asks for: Insert or Ctrl+Enter adds a row below,
 * Alt+Shift+→/← indent and outdent (Microsoft Project's keys), Ctrl+Delete
 * (Cmd+Delete on a Mac, whose Delete key sends Backspace) deletes and
 * Ctrl+Shift+Enter opens the row. Cmd stands in for Ctrl on a Mac.
 */
export function rowActionFor(e: {
  key: string
  ctrlKey: boolean
  metaKey: boolean
  altKey: boolean
  shiftKey: boolean
}): RowActionKey | null {
  const mod = e.ctrlKey || e.metaKey
  switch (e.key) {
    case 'Insert':
      return !mod && !e.altKey && !e.shiftKey ? 'insert' : null
    case 'Enter':
      if (!mod || e.altKey) return null
      return e.shiftKey ? 'open' : 'insert'
    case 'Delete':
      return mod && !e.altKey && !e.shiftKey ? 'delete' : null
    // A Mac's Delete key sends Backspace.
    case 'Backspace':
      return e.metaKey && !e.ctrlKey && !e.altKey && !e.shiftKey
        ? 'delete'
        : null
    case 'ArrowRight':
    case 'ArrowLeft':
      if (!e.altKey || !e.shiftKey || mod) return null
      return e.key === 'ArrowRight' ? 'indent' : 'outdent'
    default:
      return null
  }
}

/** Where a keyboard move puts a row: under `parentId`, at the 1-based `order`. */
export interface TreeMove<T extends TreeNode = TreeNode> {
  parentId: string | null
  /** The new parent, or null at the root. */
  parent: T | null
  order: number
}

/** The row's siblings in data order, the row included and drafts left out. */
const siblingsOf = <T extends TreeNode>(
  flat: FlattenedTreeNode<T>[],
  entry: FlattenedTreeNode<T>,
  isDraft: (id: string) => boolean,
) =>
  flat.filter(
    (other) => other.parentId === entry.parentId && !isDraft(other.node.id),
  )

/** Indenting makes the row the last child of the sibling above it; a row
 *  with no sibling above it stays where it is. */
export function indentMove<T extends TreeNode>(
  flat: FlattenedTreeNode<T>[],
  id: string,
  isDraft: (id: string) => boolean,
): TreeMove<T> | null {
  const entry = flat.find((other) => other.node.id === id)
  if (!entry || isDraft(id)) return null
  const siblings = siblingsOf(flat, entry, isDraft)
  const above = siblings[siblings.indexOf(entry) - 1]
  if (!above) return null
  const children = (above.node.children ?? []).filter(
    (child) => !isDraft(child.id),
  )
  return {
    parentId: above.node.id,
    parent: above.node,
    order: children.length + 1,
  }
}

/** Outdenting puts the row just after its parent, among the parent's
 *  siblings; a root row stays where it is. */
export function outdentMove<T extends TreeNode>(
  flat: FlattenedTreeNode<T>[],
  id: string,
  isDraft: (id: string) => boolean,
): TreeMove<T> | null {
  const entry = flat.find((other) => other.node.id === id)
  if (!entry || isDraft(id) || entry.parentId === null) return null
  const parentEntry = flat.find((other) => other.node.id === entry.parentId)
  if (!parentEntry) return null
  const parentSiblings = siblingsOf(flat, parentEntry, isDraft)
  const grandparent =
    parentEntry.parentId === null
      ? null
      : (flat.find((other) => other.node.id === parentEntry.parentId)?.node ??
        null)
  return {
    parentId: parentEntry.parentId,
    parent: grandparent,
    order: parentSiblings.indexOf(parentEntry) + 2,
  }
}

/** The 1-based place just after `id` among its siblings, or undefined when
 *  `id` is not in the tree. */
export function orderAfter<T extends TreeNode>(
  nodes: T[],
  id: string,
): number | undefined {
  const index = nodes.findIndex((node) => node.id === id)
  if (index >= 0) return index + 2
  for (const node of nodes) {
    const found = orderAfter((node.children ?? []) as T[], id)
    if (found !== undefined) return found
  }
  return undefined
}
