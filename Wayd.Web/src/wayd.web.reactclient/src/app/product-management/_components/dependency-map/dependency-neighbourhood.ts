import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid-core/grid-sorting'
import {
  DependencyStrength,
  NavigationDto,
  ProductDependenciesDto,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import type { Edge, Node } from '@xyflow/react'

/** Which column a node sits in, relative to the product the map is centred on. */
export type DependencyNodeSide = 'usedBy' | 'center' | 'dependsOn'

/** Which links the map draws: all of them, or only those a product cannot work without. */
export type DependencyStrengthFilter = 'all' | 'hard'

export interface DependencyNodeData extends Record<string, unknown> {
  label: string
  side: DependencyNodeSide
  /** The product's key, which the node links to. */
  productKey: number
  /** Set on the product the map is centred on, which reads as the subject rather than a neighbour. */
  isSubject?: boolean
}

export interface DependencyGroupData extends Record<string, unknown> {
  label: string
  productKey: number
}

export interface DependencyEdgeData extends Record<string, unknown> {
  strength: DependencyStrength
}

export type DependencyNode = Node<DependencyNodeData | DependencyGroupData>
export type DependencyEdge = Edge<DependencyEdgeData>

export interface DependencyNeighbourhood {
  nodes: DependencyNode[]
  edges: DependencyEdge[]
  /** Links left off the map, on either side. Zero when everything fits. */
  hiddenCount: number
  /**
   * What the canvas needs to draw this graph at full size, for the caller to give it. A fixed height
   * leaves a product with two links sitting in an empty field.
   */
  height: number
  /** Whether anything is drawn inside a box, which the caption explains only when it happens. */
  hasContainedProducts: boolean
}

export interface BuildDependencyNeighbourhoodOptions {
  productId: string
  productName: string
  productKey: number
  dependencies: ProductDependenciesDto | undefined
  /** Products drawn per side before the rest collapse into a count. */
  maxPerSide?: number
  strengthFilter?: DependencyStrengthFilter
}

const LEAF_WIDTH = 180
const LEAF_HEIGHT = 48
const ROW_GAP = 20
const GROUP_PADDING_X = 12
const GROUP_HEADER = 30
const GROUP_PADDING_BOTTOM = 12
const COLUMN_GAP = 140

const MIN_CANVAS_HEIGHT = 240
const MAX_CANVAS_HEIGHT = 720
const CANVAS_MARGIN = 48

/**
 * Node ids are scoped by column, because the same product can be on both sides of a map: a platform
 * whose service you depend on may also consume one of yours. Sharing one node would drag a box of
 * consumers into the providers column, where an arrow pointing back the way it came is the only clue
 * that a product sits on the wrong side.
 */
export const sideNodeId = (side: DependencyNodeSide, productId: string) =>
  side === 'center' ? productId : `${side}:${productId}`

export const groupNodeId = (side: DependencyNodeSide, productId: string) =>
  `group:${sideNodeId(side, productId)}`

/**
 * Tall enough for the graph, within bounds: below the floor it is cramped, above it the map is more page
 * than a reader wants to scroll past. Nesting made graphs much taller than the flat version, so a low
 * ceiling would zoom every product line's map down to unreadable. The canvas is capped again in CSS
 * against the viewport, which this cannot know.
 */
const canvasHeight = (contentHeight: number) =>
  Math.min(
    MAX_CANVAS_HEIGHT,
    Math.max(MIN_CANVAS_HEIGHT, contentHeight + CANVAS_MARGIN),
  )

/**
 * A product on the map, with whatever of its own products are also on the map beneath it.
 *
 * `holdsLink` and children are independent: a product can both hold a dependency of its own and contain
 * another that holds one, which is drawn as a box with the product's own node inside it.
 */
interface TreeNode {
  id: string
  label: string
  productKey: number
  holdsLink: boolean
  isSubject: boolean
  children: Map<string, TreeNode>
}

const emptyNode = (
  product: { id: string; name: string; key: number },
  isSubject = false,
): TreeNode => ({
  id: product.id,
  label: product.name,
  productKey: product.key,
  holdsLink: false,
  isSubject,
  children: new Map(),
})

/** Files a product under the chain of products it sits inside, creating the boxes on the way down. */
const insert = (
  root: TreeNode,
  path: NavigationDto[],
  product: NavigationDto,
) => {
  let node = root

  for (const step of path) {
    node = node.children.get(step.id) ?? add(node, step)
  }

  if (product.id === root.id) {
    root.holdsLink = true
    return
  }

  add(node, product).holdsLink = true
}

const add = (parent: TreeNode, product: NavigationDto): TreeNode => {
  const existing = parent.children.get(product.id)
  if (existing) return existing

  const child = emptyNode(product)
  parent.children.set(product.id, child)

  return child
}

const childrenInOrder = (node: TreeNode) =>
  [...node.children.values()].sort((a, b) =>
    caseInsensitiveCompare(a.label, b.label),
  )

interface Size {
  width: number
  height: number
}

/**
 * How much room a product needs: a leaf is one node, a box is its header plus everything inside it.
 *
 * Measured before anything is placed, because a box cannot be positioned until its size is known and its
 * size depends on its deepest child.
 */
const measure = (node: TreeNode): Size => {
  const children = childrenInOrder(node)
  if (children.length === 0) {
    return { width: LEAF_WIDTH, height: LEAF_HEIGHT }
  }

  // The product's own node sits inside its box, above the products it contains.
  const rows = [
    ...(node.holdsLink ? [{ width: LEAF_WIDTH, height: LEAF_HEIGHT }] : []),
    ...children.map(measure),
  ]

  return {
    width: Math.max(...rows.map((row) => row.width)) + GROUP_PADDING_X * 2,
    height:
      GROUP_HEADER +
      rows.reduce((total, row) => total + row.height, 0) +
      ROW_GAP * (rows.length - 1) +
      GROUP_PADDING_BOTTOM,
  }
}

/**
 * Turns a measured tree into React Flow nodes.
 *
 * A box is emitted before the nodes inside it, which React Flow requires, and those carry positions
 * relative to it rather than to the canvas.
 */
const place = (
  node: TreeNode,
  side: DependencyNodeSide,
  x: number,
  y: number,
  parentId: string | undefined,
  into: DependencyNode[],
) => {
  const children = childrenInOrder(node)

  const leaf = (at: { x: number; y: number }) =>
    into.push({
      id: sideNodeId(side, node.id),
      type: 'product',
      position: at,
      ...(parentId ? { parentId, extent: 'parent' as const } : {}),
      data: {
        label: node.label,
        side,
        productKey: node.productKey,
        ...(node.isSubject ? { isSubject: true } : {}),
      },
    })

  if (children.length === 0) {
    leaf({ x, y })
    return
  }

  const size = measure(node)
  const groupId = groupNodeId(side, node.id)

  into.push({
    id: groupId,
    type: 'productGroup',
    position: { x, y },
    // Sized through width/height rather than style: both render the same box, but only these are read
    // back off the node, and the image export sizes the box from what it reads.
    width: size.width,
    height: size.height,
    ...(parentId ? { parentId, extent: 'parent' as const } : {}),
    data: { label: node.label, productKey: node.productKey },
  })

  let offsetY = GROUP_HEADER

  if (node.holdsLink) {
    into.push({
      id: sideNodeId(side, node.id),
      type: 'product',
      position: { x: GROUP_PADDING_X, y: offsetY },
      parentId: groupId,
      extent: 'parent',
      data: {
        label: node.label,
        side,
        productKey: node.productKey,
        ...(node.isSubject ? { isSubject: true } : {}),
      },
    })
    offsetY += LEAF_HEIGHT + ROW_GAP
  }

  for (const child of children) {
    place(child, side, GROUP_PADDING_X, offsetY, groupId, into)
    offsetY += measure(child).height + ROW_GAP
  }
}

/** Stacks a column's products, and reports how tall and wide the column ended up. */
const stack = (
  roots: TreeNode[],
  side: DependencyNodeSide,
  into: DependencyNode[],
): Size & { place: (x: number, offsetY: number) => void } => {
  const sizes = roots.map(measure)
  const height =
    sizes.reduce((total, size) => total + size.height, 0) +
    ROW_GAP * Math.max(0, roots.length - 1)
  const width = roots.length === 0 ? 0 : Math.max(...sizes.map((s) => s.width))

  return {
    width,
    height,
    place: (x, offsetY) => {
      let y = offsetY
      roots.forEach((root, index) => {
        place(root, side, x, y, undefined, into)
        y += sizes[index].height + ROW_GAP
      })
    },
  }
}

/**
 * The map of what a product relies on and what relies on it, one hop out.
 *
 * Ended links are left out whatever the caller passed: a map reads as what is true now, and a link that
 * has stopped says nothing about today's blast radius. The Dependencies section is where history is read.
 *
 * Every product is drawn inside the products that contain it, on both sides: a link rolled up from a
 * descendant starts at that descendant, nested under whatever it belongs to, and a service depended on is
 * drawn under its own platform. Containment is a box rather than an edge, because an edge would put
 * "is part of" and "relies on" in the same visual language — and crediting a parent with its child's link
 * would claim a dependency the parent does not hold.
 */
export const buildDependencyNeighbourhood = ({
  productId,
  productName,
  productKey,
  dependencies,
  maxPerSide = 6,
  strengthFilter = 'all',
}: BuildDependencyNeighbourhoodOptions): DependencyNeighbourhood => {
  // Filtered before anything is grouped or capped. Products and boxes exist only because a link put them
  // there, so one left with no links is never drawn; and soft links cannot take the slots the cap would
  // otherwise leave for hard ones.
  const open = (links: ProductDependencyDto[] | undefined) =>
    (links ?? []).filter(
      (d) =>
        !d.endsOn &&
        (strengthFilter === 'all' || d.strength === DependencyStrength.Hard),
    )

  const sides = [
    { side: 'dependsOn' as const, links: open(dependencies?.dependsOn) },
    { side: 'usedBy' as const, links: open(dependencies?.usedBy) },
  ]

  const subject = emptyNode(
    { id: productId, name: productName, key: productKey },
    true,
  )
  const farRoots = new Map<
    string,
    { side: DependencyNodeSide; root: TreeNode }
  >()

  const edges: DependencyEdge[] = []
  let hiddenCount = 0

  for (const { side, links } of sides) {
    const ends = (link: ProductDependencyDto) =>
      side === 'dependsOn'
        ? {
            far: link.dependsOnProduct,
            // Defaulted because a cached response written before the paths existed would otherwise take
            // the whole section down rather than drawing a flatter map.
            farPath: link.dependsOnProductPath ?? [],
            near: link.product,
            nearPath: link.productPath ?? [],
          }
        : {
            far: link.product,
            farPath: link.productPath ?? [],
            near: link.dependsOnProduct,
            nearPath: link.dependsOnProductPath ?? [],
          }

    // Grouped before the cap, so it limits products drawn rather than links: cutting mid-product would
    // show a node missing the very edges that explain it.
    const byFarEnd = new Map<string, ProductDependencyDto[]>()
    for (const link of links) {
      const id = ends(link).far.id
      byFarEnd.set(id, [...(byFarEnd.get(id) ?? []), link])
    }

    const shown = [...byFarEnd.values()].slice(0, maxPerSide)
    hiddenCount += [...byFarEnd.values()]
      .slice(maxPerSide)
      .reduce((total, group) => total + group.length, 0)

    for (const group of shown) {
      for (const link of group) {
        const { far, farPath, near, nearPath } = ends(link)

        // The near end's chain is trimmed to the part inside this product; everything above it is the
        // product's own ancestry, which the map is not about. A link the product holds itself carries
        // the product's own ancestry and nothing below it — and a product is not in its own path, so
        // an untrimmed chain would build the product's parents inside its box, upside down.
        const depth = nearPath.findIndex((step) => step.id === productId)
        const inside = depth === -1 ? [] : nearPath.slice(depth + 1)
        insert(subject, inside, near)

        // Keyed by side as well as product: a product that both depends on this one and is depended on
        // by it belongs in both columns, drawn once in each.
        const farRootKey = `${side}:${farPath[0]?.id ?? far.id}`
        const farRoot = farRoots.get(farRootKey) ?? {
          side,
          root: emptyNode(farPath[0] ?? far, false),
        }
        farRoots.set(farRootKey, farRoot)
        insert(farRoot.root, farPath.slice(1), far)

        edges.push({
          id: link.id,
          source: side === 'dependsOn' ? near.id : sideNodeId(side, far.id),
          target: side === 'dependsOn' ? sideNodeId(side, far.id) : near.id,
          data: { strength: link.strength },
        })
      }
    }
  }

  if (edges.length === 0) {
    return {
      nodes: [],
      edges: [],
      hiddenCount: 0,
      height: 0,
      hasContainedProducts: false,
    }
  }

  const nodes: DependencyNode[] = []
  const onSide = (side: DependencyNodeSide) =>
    [...farRoots.values()]
      .filter((entry) => entry.side === side)
      .map((entry) => entry.root)

  const usedByRoots = onSide('usedBy')
  const dependsOnRoots = onSide('dependsOn')

  const left = stack(usedByRoots, 'usedBy', nodes)
  const centre = stack([subject], 'center', nodes)
  const right = stack(dependsOnRoots, 'dependsOn', nodes)

  const tallest = Math.max(left.height, centre.height, right.height)
  const centred = (column: Size) => (tallest - column.height) / 2

  const leftX = 0
  const centreX = left.width + (left.width > 0 ? COLUMN_GAP : 0)
  const rightX = centreX + centre.width + COLUMN_GAP

  left.place(leftX, centred(left))
  centre.place(centreX, centred(centre))
  right.place(rightX, centred(right))

  return {
    nodes,
    edges,
    hiddenCount,
    height: canvasHeight(tallest),
    hasContainedProducts: nodes.some((node) => node.type === 'productGroup'),
  }
}
