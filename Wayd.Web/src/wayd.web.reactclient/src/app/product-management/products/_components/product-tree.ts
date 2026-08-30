import { ProductDto } from '@/src/services/wayd-api'

/**
 * A product with its children attached, for the grid's tree mode.
 */
export interface ProductTreeNode extends ProductDto {
  children: ProductTreeNode[]
}

/**
 * Nests a flat product list by parent.
 *
 * A node whose parent is not in the list is treated as a root rather than dropped — the list may be
 * filtered, and silently hiding a product because its parent was filtered out would misreport what
 * exists. Any cycle in the data leaves the affected nodes at the root for the same reason: showing
 * them detached beats showing nothing or recursing forever.
 */
export const buildProductTree = (products: ProductDto[]): ProductTreeNode[] => {
  const nodes = new Map<string, ProductTreeNode>(
    products.map((p) => [p.id, { ...p, children: [] }]),
  )

  const roots: ProductTreeNode[] = []

  for (const node of nodes.values()) {
    const parentId = node.parent?.id
    const parent = parentId ? nodes.get(parentId) : undefined

    if (parent && !createsCycle(node, parent, nodes)) {
      parent.children.push(node)
    } else {
      roots.push(node)
    }
  }

  return roots
}

/**
 * Whether attaching a node under a candidate parent would close a loop.
 *
 * The domain refuses to create one, so this guards against data that is already wrong rather than
 * against normal use — but an unguarded walk would hang the browser rather than show the problem.
 */
const createsCycle = (
  node: ProductTreeNode,
  parent: ProductTreeNode,
  nodes: Map<string, ProductTreeNode>,
): boolean => {
  const seen = new Set<string>([node.id])
  let current: ProductTreeNode | undefined = parent

  while (current) {
    if (seen.has(current.id)) return true
    seen.add(current.id)

    const nextId: string | undefined = current.parent?.id
    current = nextId ? nodes.get(nextId) : undefined
  }

  return false
}
