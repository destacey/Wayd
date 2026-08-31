import { ProductDto } from '@/src/services/wayd-api'
import {
  buildMoveTargetTree,
  buildProductTree,
  ProductTreeNode,
} from './product-tree'

const product = (
  id: string,
  name: string,
  parent?: { id: string; key: number; name: string },
): ProductDto =>
  ({
    id,
    key: Number(id),
    name,
    parent,
    tags: [],
  }) as unknown as ProductDto

describe('buildProductTree', () => {
  it('nests a child under its parent', () => {
    const suite = product('1', 'Suite')
    const checkout = product('2', 'Checkout', { id: '1', key: 1, name: 'Suite' })

    const tree = buildProductTree([suite, checkout])

    expect(tree).toHaveLength(1)
    expect(tree[0].name).toBe('Suite')
    expect(tree[0].children.map((c) => c.name)).toEqual(['Checkout'])
  })

  it('returns products with no parent as roots', () => {
    const tree = buildProductTree([product('1', 'A'), product('2', 'B')])

    expect(tree.map((n) => n.name).sort()).toEqual(['A', 'B'])
  })

  it('nests several levels deep', () => {
    const tree = buildProductTree([
      product('1', 'Suite'),
      product('2', 'Platform', { id: '1', key: 1, name: 'Suite' }),
      product('3', 'Checkout', { id: '2', key: 2, name: 'Platform' }),
    ])

    expect(tree[0].children[0].children[0].name).toBe('Checkout')
  })

  it('keeps a product whose parent is not in the list', () => {
    // The list may be filtered. Dropping the child because its parent was filtered out would
    // misreport what exists, so it surfaces as a root instead.
    const orphan = product('2', 'Checkout', { id: '99', key: 99, name: 'Gone' })

    const tree = buildProductTree([orphan])

    expect(tree.map((n) => n.name)).toEqual(['Checkout'])
  })

  it('does not hang on a cycle in the data', () => {
    // The domain refuses to create one, so this guards against data that is already wrong — an
    // unguarded walk would hang the browser rather than show the problem.
    const a = product('1', 'A', { id: '2', key: 2, name: 'B' })
    const b = product('2', 'B', { id: '1', key: 1, name: 'A' })

    const tree = buildProductTree([a, b])

    expect(tree.length).toBeGreaterThan(0)
    expect(tree.flatMap((n) => [n.name, ...n.children.map((c) => c.name)])).toContain('A')
  })

  it('returns nothing for an empty list', () => {
    expect(buildProductTree([])).toEqual([])
  })
})

describe('buildMoveTargetTree', () => {
  // Suite > Checkout > Payments, plus an unrelated Billing root.
  const suite = product('1', 'Suite')
  const checkout = product('2', 'Checkout', { id: '1', key: 1, name: 'Suite' })
  const payments = product('3', 'Payments', {
    id: '2',
    key: 2,
    name: 'Checkout',
  })
  const billing = product('4', 'Billing')
  const all = [suite, checkout, payments, billing]

  const names = (nodes: ProductTreeNode[]): string[] =>
    nodes.flatMap((node) => [node.name, ...names(node.children)])

  it('keeps the hierarchy so the move is made in context', () => {
    // Act
    const tree = buildMoveTargetTree(all, '4')

    // Assert
    expect(tree.map((n) => n.name)).toEqual(['Suite'])
    expect(tree[0].children.map((n) => n.name)).toEqual(['Checkout'])
  })

  it('excludes the product being moved', () => {
    // Act
    const tree = buildMoveTargetTree(all, '2')

    // Assert
    expect(names(tree)).not.toContain('Checkout')
  })

  it('excludes the descendants of the product being moved', () => {
    // A product cannot become its own ancestor, so its whole subtree is out — not just itself.
    // Act
    const tree = buildMoveTargetTree(all, '2')

    // Assert
    expect(names(tree)).not.toContain('Payments')
  })

  it('prunes the branch rather than hoisting the survivors', () => {
    // A grandchild shown at the root would read as a legal target while its parent was hidden.
    // Act
    const tree = buildMoveTargetTree(all, '2')

    // Assert
    expect(tree.map((n) => n.name).sort()).toEqual(['Billing', 'Suite'])
  })

  it('leaves everything outside the subtree selectable', () => {
    // Act
    const tree = buildMoveTargetTree(all, '2')

    // Assert
    // Moving to a sibling, to an ancestor, or to the root are all legal.
    expect(names(tree).sort()).toEqual(['Billing', 'Suite'])
  })

  it('offers every other product when moving a leaf', () => {
    // Act
    const tree = buildMoveTargetTree(all, '3')

    // Assert
    expect(names(tree).sort()).toEqual(['Billing', 'Checkout', 'Suite'])
  })
})
