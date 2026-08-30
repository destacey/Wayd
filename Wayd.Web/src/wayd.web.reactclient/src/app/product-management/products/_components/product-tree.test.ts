import { ProductDto } from '@/src/services/wayd-api'
import { buildProductTree } from './product-tree'

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
