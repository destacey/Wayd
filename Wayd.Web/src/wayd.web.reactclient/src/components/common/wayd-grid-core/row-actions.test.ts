import type { TreeNode } from './dnd/tree-projection'
import {
  indentMove,
  orderAfter,
  outdentMove,
  rowActionFor,
} from './row-actions'
import { flattenTree } from './tree-utils'

const key = (
  name: string,
  mods: Partial<
    Record<'ctrlKey' | 'metaKey' | 'altKey' | 'shiftKey', boolean>
  > = {},
) => ({
  key: name,
  ctrlKey: false,
  metaKey: false,
  altKey: false,
  shiftKey: false,
  ...mods,
})

const node = (id: string, children: TreeNode[] = []): TreeNode => ({
  id,
  children,
})

// a
//   a1
//   a2
//     a2x
//   draft-1
//   a3
// b
const TREE = [
  node('a', [
    node('a1'),
    node('a2', [node('a2x')]),
    node('draft-1'),
    node('a3'),
  ]),
  node('b'),
]
const FLAT = flattenTree(TREE)
const isDraft = (id: string) => id.startsWith('draft-')

describe('rowActionFor', () => {
  it.each([
    ['Insert', {}, 'insert'],
    ['Enter', { ctrlKey: true }, 'insert'],
    ['Enter', { metaKey: true }, 'insert'],
    ['Enter', { ctrlKey: true, shiftKey: true }, 'open'],
    ['Delete', { ctrlKey: true }, 'delete'],
    ['Delete', { metaKey: true }, 'delete'],
    ['Backspace', { metaKey: true }, 'delete'],
    ['ArrowRight', { altKey: true, shiftKey: true }, 'indent'],
    ['ArrowLeft', { altKey: true, shiftKey: true }, 'outdent'],
  ] as const)('reads %s %o as %s', (name, mods, action) => {
    // Arrange / Act
    const result = rowActionFor(key(name, mods))

    // Assert
    expect(result).toBe(action)
  })

  it.each([
    ['Insert', { shiftKey: true }],
    ['Insert', { ctrlKey: true }],
    ['Enter', {}],
    ['Enter', { ctrlKey: true, altKey: true }],
    ['Delete', {}],
    ['Delete', { ctrlKey: true, shiftKey: true }],
    ['Backspace', {}],
    ['Backspace', { ctrlKey: true }],
    ['ArrowRight', { altKey: true }],
    ['ArrowRight', { shiftKey: true }],
    ['ArrowLeft', { ctrlKey: true, altKey: true, shiftKey: true }],
    ['a', { ctrlKey: true }],
  ] as const)('reads %s %o as no row action', (name, mods) => {
    // Arrange / Act
    const result = rowActionFor(key(name, mods))

    // Assert
    expect(result).toBeNull()
  })
})

describe('indentMove', () => {
  it('makes the row the last child of the sibling above it', () => {
    // Arrange / Act
    const move = indentMove(FLAT, 'a2', isDraft)

    // Assert
    expect(move).toStrictEqual({
      parentId: 'a1',
      parent: TREE[0].children[0],
      order: 1,
    })
  })

  it('counts the new parent’s existing children', () => {
    // Arrange / Act — a3's sibling above, past the draft, is a2 with one child
    const move = indentMove(FLAT, 'a3', isDraft)

    // Assert
    expect(move?.parentId).toBe('a2')
    expect(move?.order).toBe(2)
  })

  it('does not indent the first child', () => {
    // Arrange / Act / Assert
    expect(indentMove(FLAT, 'a1', isDraft)).toBeNull()
  })

  it('does not indent a draft', () => {
    // Arrange / Act / Assert
    expect(indentMove(FLAT, 'draft-1', isDraft)).toBeNull()
  })
})

describe('outdentMove', () => {
  it('puts the row just after its parent', () => {
    // Arrange / Act
    const move = outdentMove(FLAT, 'a2x', isDraft)

    // Assert
    expect(move).toStrictEqual({ parentId: 'a', parent: TREE[0], order: 3 })
  })

  it('moves a child of a root row to the root', () => {
    // Arrange / Act
    const move = outdentMove(FLAT, 'a3', isDraft)

    // Assert
    expect(move).toStrictEqual({ parentId: null, parent: null, order: 2 })
  })

  it('does not outdent a root row', () => {
    // Arrange / Act / Assert
    expect(outdentMove(FLAT, 'b', isDraft)).toBeNull()
  })
})

describe('orderAfter', () => {
  it('is the 1-based place just after the row among its siblings', () => {
    // Arrange / Act / Assert
    expect(orderAfter(TREE, 'a1')).toBe(2)
    expect(orderAfter(TREE, 'a2x')).toBe(2)
    expect(orderAfter(TREE, 'b')).toBe(3)
  })

  it('is undefined for a row not in the tree', () => {
    // Arrange / Act / Assert
    expect(orderAfter(TREE, 'gone')).toBeUndefined()
  })
})
