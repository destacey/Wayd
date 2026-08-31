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
 * The tree of products a node may be moved under, with itself and everything beneath it removed.
 *
 * A product cannot become its own ancestor, so its whole subtree is out — not just the product
 * itself. The branch is pruned rather than having its survivors hoisted: a grandchild shown at the
 * root would read as a legal target while its parent was hidden, which both misstates the hierarchy
 * and offers a move the API would refuse anyway.
 *
 * The domain enforces this regardless. Doing it here as well means the refusal is never reached by
 * anyone using the form as intended, which is what a picker is for.
 */
export const buildMoveTargetTree = (
  products: ProductDto[],
  movingProductId: string,
): ProductTreeNode[] => prune(buildProductTree(products), movingProductId)

const prune = (
  nodes: ProductTreeNode[],
  excludedId: string,
): ProductTreeNode[] =>
  nodes
    .filter((node) => node.id !== excludedId)
    .map((node) => ({ ...node, children: prune(node.children, excludedId) }))

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
