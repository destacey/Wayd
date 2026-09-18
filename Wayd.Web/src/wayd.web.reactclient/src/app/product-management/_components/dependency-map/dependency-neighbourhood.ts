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

/** The two sides a map grows along: what relies on the subject, and what the subject relies on. */
export type DependencyFarSide = Exclude<DependencyNodeSide, 'center'>

/** Which links the map draws: all of them, or only those a product cannot work without. */
export type DependencyStrengthFilter = 'all' | 'hard'

/**
 * Where a product off to one side stands with its own links: not asked for, being fetched, drawn, found
 * to have nothing further out, or failed to load.
 */
export type DependencyExpansionStatus =
  'collapsed' | 'loading' | 'expanded' | 'empty' | 'error'

export interface DependencyNodeData extends Record<string, unknown> {
  label: string
  side: DependencyNodeSide
  productId: string
  /** The product's key, which the node links to. */
  productKey: number
  /** Set on the product the map is centred on, which reads as the subject rather than a neighbour. */
  isSubject?: boolean
  /** Set on every product off to one side, which can be expanded; never on the centre column. */
  expansion?: DependencyExpansionStatus
}

export interface DependencyGroupData extends Record<string, unknown> {
  label: string
  productKey: number
}

/** The count of products an expansion, or the subject, left off a column. */
export interface DependencyOverflowData extends Record<string, unknown> {
  label: string
  side: DependencyFarSide
  /** The product whose links overflowed, or null for the subject's own. */
  ownerId: string | null
  count: number
}

export interface DependencyEdgeData extends Record<string, unknown> {
  strength: DependencyStrength
}

export type DependencyNode = Node<
  DependencyNodeData | DependencyGroupData | DependencyOverflowData
>
export type DependencyEdge = Edge<DependencyEdgeData>

/** A product the reader expanded, with whatever has loaded for it. */
export interface DependencyExpansion {
  /** Undefined while loading. */
  dependencies?: ProductDependenciesDto
  isError?: boolean
  /** Lifts the per-expansion cap, once the reader has asked for the rest. */
  showAll?: boolean
}

export type DependencyExpansions = Record<
  DependencyFarSide,
  Record<string, DependencyExpansion>
>

export interface DependencyNeighbourhood {
  nodes: DependencyNode[]
  edges: DependencyEdge[]
  /** Links the subject's own columns left off, on either side. Zero when everything fits. */
  hiddenCount: number
  /**
   * What the canvas needs to draw this graph at full size, for the caller to give it. A fixed height
   * leaves a product with two links sitting in an empty field.
   */
  height: number
  /**
   * Whether the subject is drawn as a box holding the products beneath it, which the caption explains
   * only when it happens. Boxes on the far sides are the other products' own platforms, which the
   * caption's sentence does not describe.
   */
  hasContainedProducts: boolean
  /** The products drawn on each side, which is what an expansion must still reach to stay open. */
  placed: Record<DependencyFarSide, string[]>
}

export interface BuildDependencyNeighbourhoodOptions {
  productId: string
  productName: string
  productKey: number
  dependencies: ProductDependenciesDto | undefined
  /** Products drawn per column, per product whose links fill it, before the rest collapse into a count. */
  maxPerSide?: number
  strengthFilter?: DependencyStrengthFilter
  expansions?: DependencyExpansions
  /** Sides where the subject's own overflow has been revealed. */
  subjectShowAll?: DependencyFarSide[]
}

const LEAF_WIDTH = 180
const LEAF_HEIGHT = 48
const OVERFLOW_HEIGHT = 32
const ROW_GAP = 20
const GROUP_PADDING_X = 12
const GROUP_HEADER = 30
const GROUP_PADDING_BOTTOM = 12
const COLUMN_GAP = 140

const MIN_CANVAS_HEIGHT = 240
const MAX_CANVAS_HEIGHT = 720
const CANVAS_MARGIN = 48

const FAR_SIDES: DependencyFarSide[] = ['dependsOn', 'usedBy']

/**
 * Node ids are scoped by side, because the same product can be on both sides of a map: a platform
 * whose service you depend on may also consume one of yours. Sharing one node would drag a box of
 * consumers into the providers column, where an arrow pointing back the way it came is the only clue
 * that a product sits on the wrong side. Within a side a product is drawn once, however many columns
 * reach it.
 */
export const sideNodeId = (side: DependencyNodeSide, productId: string) =>
  side === 'center' ? productId : `${side}:${productId}`

/**
 * Boxes are scoped by column as well as side: a platform can hold one product found in the first column
 * and another found two columns out, and each column draws its own box.
 */
export const groupNodeId = (
  side: DependencyNodeSide,
  productId: string,
  column = 1,
) =>
  column === 1
    ? `group:${sideNodeId(side, productId)}`
    : `group:${side}:${column}:${productId}`

export const overflowNodeId = (
  side: DependencyFarSide,
  ownerId: string | null,
) => `overflow:${side}:${ownerId ?? 'subject'}`

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
  expansion?: DependencyExpansionStatus
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

/**
 * Files a product under the chain of products it sits inside, creating the boxes on the way down, and
 * returns the product's own node.
 */
const insert = (
  root: TreeNode,
  path: NavigationDto[],
  product: NavigationDto,
): TreeNode => {
  let node = root

  for (const step of path) {
    node = node.children.get(step.id) ?? add(node, step)
  }

  if (product.id === root.id) {
    root.holdsLink = true
    return root
  }

  const own = add(node, product)
  own.holdsLink = true

  return own
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

/** The products a tree draws as nodes, top to bottom, which is the order a column reads in. */
const productsInOrder = (node: TreeNode): string[] => [
  ...(node.holdsLink ? [node.id] : []),
  ...childrenInOrder(node).flatMap(productsInOrder),
]

/**
 * Turns a measured tree into React Flow nodes.
 *
 * A box is emitted before the nodes inside it, which React Flow requires, and those carry positions
 * relative to it rather than to the canvas.
 */
const place = (
  node: TreeNode,
  side: DependencyNodeSide,
  column: number,
  x: number,
  y: number,
  parentId: string | undefined,
  into: DependencyNode[],
) => {
  const children = childrenInOrder(node)

  const productNode = (
    at: { x: number; y: number },
    parent: string | undefined,
  ): DependencyNode => ({
    id: sideNodeId(side, node.id),
    type: 'product',
    position: at,
    ...(parent ? { parentId: parent, extent: 'parent' as const } : {}),
    data: {
      label: node.label,
      side,
      productId: node.id,
      productKey: node.productKey,
      ...(node.isSubject ? { isSubject: true } : {}),
      ...(node.expansion ? { expansion: node.expansion } : {}),
    },
  })

  if (children.length === 0) {
    into.push(productNode({ x, y }, parentId))
    return
  }

  const size = measure(node)
  const groupId = groupNodeId(side, node.id, column)

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
    into.push(productNode({ x: GROUP_PADDING_X, y: offsetY }, groupId))
    offsetY += LEAF_HEIGHT + ROW_GAP
  }

  for (const child of children) {
    place(child, side, column, GROUP_PADDING_X, offsetY, groupId, into)
    offsetY += measure(child).height + ROW_GAP
  }
}

interface Overflow {
  ownerId: string | null
  label: string
  count: number
  /** The owner's position in the column before, so the count sits beside what it counts. */
  rank: number
}

/** One column on one side: the products it found, boxed by what they belong to, and what it left off. */
interface Column {
  roots: Map<string, { root: TreeNode; rank: number; order: number }>
  overflows: Overflow[]
}

/** Stacks a column's products, and reports how tall and wide the column ended up. */
const stack = (
  column: Column,
  side: DependencyNodeSide,
  columnNumber: number,
  into: DependencyNode[],
): Size & { place: (x: number, offsetY: number) => void } => {
  const roots = orderedRoots(column)
  const overflows = [...column.overflows].sort((a, b) => a.rank - b.rank)

  const rows = [
    ...roots.map(measure),
    ...overflows.map(() => ({ width: LEAF_WIDTH, height: OVERFLOW_HEIGHT })),
  ]
  const height =
    rows.reduce((total, size) => total + size.height, 0) +
    ROW_GAP * Math.max(0, rows.length - 1)
  const width = rows.length === 0 ? 0 : Math.max(...rows.map((s) => s.width))

  return {
    width,
    height,
    place: (x, offsetY) => {
      let y = offsetY
      roots.forEach((root, index) => {
        place(root, side, columnNumber, x, y, undefined, into)
        y += rows[index].height + ROW_GAP
      })
      for (const overflow of overflows) {
        into.push({
          id: overflowNodeId(side as DependencyFarSide, overflow.ownerId),
          type: 'overflow',
          position: { x, y },
          data: {
            label: overflow.label,
            side: side as DependencyFarSide,
            ownerId: overflow.ownerId,
            count: overflow.count,
          },
        })
        y += OVERFLOW_HEIGHT + ROW_GAP
      }
    },
  }
}

/** The connection points on every product node, which an edge names so it joins the sides it should. */
export const DEPENDENCY_HANDLES = {
  inLeft: 'in-left',
  outRight: 'out-right',
  outLeft: 'out-left',
  inRight: 'in-right',
} as const

/**
 * Which sides of its two nodes an edge joins, from the columns they sit in.
 *
 * Every link the subject holds, and every product an expansion finds, runs left to right. A product an
 * expansion reaches again keeps its column, so a link to it can run within a column or back toward the
 * subject — and one that still left the right side and entered the left would loop across the whole map
 * through every box in between. Backward, it leaves the left side and enters the right; within a column
 * it bows out on the column's outer side, away from the links that arrive from the subject.
 */
const handlesFor = (sourceColumn: number, targetColumn: number) => {
  if (sourceColumn < targetColumn) {
    return {
      sourceHandle: DEPENDENCY_HANDLES.outRight,
      targetHandle: DEPENDENCY_HANDLES.inLeft,
    }
  }
  if (sourceColumn > targetColumn) {
    return {
      sourceHandle: DEPENDENCY_HANDLES.outLeft,
      targetHandle: DEPENDENCY_HANDLES.inRight,
    }
  }
  return sourceColumn < 0
    ? {
        sourceHandle: DEPENDENCY_HANDLES.outLeft,
        targetHandle: DEPENDENCY_HANDLES.inLeft,
      }
    : {
        sourceHandle: DEPENDENCY_HANDLES.outRight,
        targetHandle: DEPENDENCY_HANDLES.inRight,
      }
}

/**
 * Beside whatever they were expanded from, then in the order the links arrived, which the API sorts by
 * name. Keeping each expansion's products near their source is what stops every edge from crossing the
 * column.
 */
const orderedRoots = (column: Column) =>
  [...column.roots.values()]
    .sort((a, b) => a.rank - b.rank || a.order - b.order)
    .map((entry) => entry.root)

/**
 * The map of what a product relies on and what relies on it: one hop out, and further along any product
 * the reader expanded.
 *
 * Ended links are left out whatever the caller passed: a map reads as what is true now, and a link that
 * has stopped says nothing about today's blast radius. The Dependencies section is where history is read.
 *
 * Every product is drawn inside the products that contain it, on both sides: a link rolled up from a
 * descendant starts at that descendant, nested under whatever it belongs to, and a service depended on is
 * drawn under its own platform. Containment is a box rather than an edge, because an edge would put
 * "is part of" and "relies on" in the same visual language — and crediting a parent with its child's link
 * would claim a dependency the parent does not hold.
 *
 * An expansion only ever grows outward, along the side its product is on: what relies on a consumer, or
 * what a provider relies on. It draws only the links that product holds itself, because what breaks when
 * it goes down is what relies on it, not on its children — the endpoint's rollup of its subtree is left
 * for its own map. A product reached again on the same side keeps the column it was first found in, so
 * expanding one product never moves another.
 */
export const buildDependencyNeighbourhood = ({
  productId,
  productName,
  productKey,
  dependencies,
  maxPerSide = 6,
  strengthFilter = 'all',
  expansions,
  subjectShowAll = [],
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

  const subject = emptyNode(
    { id: productId, name: productName, key: productKey },
    true,
  )

  const edges = new Map<string, DependencyEdge>()
  const columns: Record<DependencyFarSide, Column[]> = {
    usedBy: [],
    dependsOn: [],
  }
  const placed: Record<DependencyFarSide, Map<string, TreeNode>> = {
    usedBy: new Map(),
    dependsOn: new Map(),
  }
  // Signed, left of the subject negative: which way an edge runs decides which sides of its nodes it
  // joins. Anything not in here is in the centre column.
  const columnOf = new Map<string, number>()
  let hiddenCount = 0

  const overflowLabel = (count: number, owner: string | null) =>
    `+${count} more${strengthFilter === 'hard' ? ' hard' : ''}${owner ? ` for ${owner}` : ''}`

  for (const side of FAR_SIDES) {
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

    const columnAt = (number: number) =>
      (columns[side][number - 1] ??= { roots: new Map(), overflows: [] })

    /**
     * Draws one product's links into the column beyond it. `owner` is null for the subject, whose links
     * start inside the centre box rather than at a node already on this side.
     */
    const grow = (
      owner: TreeNode | null,
      links: ProductDependencyDto[],
      columnNumber: number,
      rank: number,
      showAll: boolean,
    ) => {
      // Grouped before the cap, so it limits products drawn rather than links: cutting mid-product would
      // show a node missing the very edges that explain it.
      const byFarEnd = new Map<string, ProductDependencyDto[]>()
      for (const link of links) {
        const id = ends(link).far.id
        byFarEnd.set(id, [...(byFarEnd.get(id) ?? []), link])
      }

      const groups = [...byFarEnd.values()]
      const shown = showAll ? groups : groups.slice(0, maxPerSide)
      const hidden = groups
        .slice(shown.length)
        .reduce((total, group) => total + group.length, 0)

      const column = columnAt(columnNumber)
      if (hidden > 0) {
        column.overflows.push({
          ownerId: owner?.id ?? null,
          label: overflowLabel(hidden, owner?.label ?? null),
          count: hidden,
          rank,
        })
        if (!owner) hiddenCount += hidden
      }

      for (const group of shown) {
        for (const link of group) {
          const { far, farPath, near, nearPath } = ends(link)

          if (!owner) {
            // The near end's chain is trimmed to the part inside this product; everything above it is
            // the product's own ancestry, which the map is not about. A link the product holds itself
            // carries the product's own ancestry and nothing below it — and a product is not in its own
            // path, so an untrimmed chain would build the product's parents inside its box, upside down.
            const depth = nearPath.findIndex((step) => step.id === productId)
            const inside = depth === -1 ? [] : nearPath.slice(depth + 1)
            insert(subject, inside, near)
          }

          if (!placed[side].has(far.id)) {
            const rootProduct = farPath[0] ?? far
            const existing = column.roots.get(rootProduct.id)
            const entry = existing ?? {
              root: emptyNode(rootProduct),
              rank,
              order: column.roots.size,
            }
            entry.rank = Math.min(entry.rank, rank)
            column.roots.set(rootProduct.id, entry)
            placed[side].set(far.id, insert(entry.root, farPath.slice(1), far))
            columnOf.set(
              sideNodeId(side, far.id),
              side === 'dependsOn' ? columnNumber : -columnNumber,
            )
          }

          const nearId = owner ? sideNodeId(side, owner.id) : near.id
          const farId = sideNodeId(side, far.id)

          // One edge per link. A link can be reached from both sides in a cycle, where it joins
          // different nodes, so only a repeat within a side is dropped.
          const key = `${side}:${link.id}`
          if (edges.has(key)) continue
          edges.set(key, {
            id: [...edges.values()].some((e) => e.id === link.id)
              ? key
              : link.id,
            source: side === 'dependsOn' ? nearId : farId,
            target: side === 'dependsOn' ? farId : nearId,
            data: { strength: link.strength },
          })
        }
      }
    }

    grow(null, open(dependencies?.[side]), 1, 0, subjectShowAll.includes(side))

    // Column by column, so each expansion knows where its source sits before its own products are
    // ordered. A product placed further out by an earlier expansion is still expanded in its own turn.
    for (let number = 1; number <= columns[side].length; number++) {
      const order = orderedRoots(columns[side][number - 1]).flatMap(
        productsInOrder,
      )

      order.forEach((id, rank) => {
        const node = placed[side].get(id)
        // A product first found in an earlier column is expanded there, not again here.
        if (!node || node.expansion) return

        const expansion = expansions?.[side][id]
        if (!expansion) {
          node.expansion = 'collapsed'
          return
        }
        if (!expansion.dependencies) {
          node.expansion = expansion.isError ? 'error' : 'loading'
          return
        }

        const isInsideSubject = (end: NavigationDto, path: NavigationDto[]) =>
          end.id === productId || path.some((step) => step.id === productId)

        const links = open(expansion.dependencies[side]).filter((link) => {
          const { far, farPath, near } = ends(link)
          // Only what the product holds itself, and never back into the subject: every link touching
          // the subject is already drawn from the centre.
          return near.id === id && !isInsideSubject(far, farPath)
        })

        node.expansion = links.length > 0 ? 'expanded' : 'empty'
        if (links.length > 0) {
          grow(node, links, number + 1, rank, expansion.showAll === true)
        }
      })
    }
  }

  if (edges.size === 0) {
    return {
      nodes: [],
      edges: [],
      hiddenCount: 0,
      height: 0,
      hasContainedProducts: false,
      placed: { usedBy: [], dependsOn: [] },
    }
  }

  const nodes: DependencyNode[] = []

  const stacks = (side: DependencyFarSide) =>
    columns[side].map((column, index) => stack(column, side, index + 1, nodes))

  // Left to right: the furthest consumers first, the furthest providers last.
  const left = stacks('usedBy').reverse()
  const centre = stack(
    {
      roots: new Map([[productId, { root: subject, rank: 0, order: 0 }]]),
      overflows: [],
    },
    'center',
    1,
    nodes,
  )
  const right = stacks('dependsOn')
  const all = [...left, centre, ...right]

  const tallest = Math.max(...all.map((column) => column.height))
  let x = 0
  for (const column of all) {
    if (column.width === 0) continue
    column.place(x, (tallest - column.height) / 2)
    x += column.width + COLUMN_GAP
  }

  return {
    nodes,
    edges: [...edges.values()].map((edge) => ({
      ...edge,
      ...handlesFor(
        columnOf.get(edge.source) ?? 0,
        columnOf.get(edge.target) ?? 0,
      ),
    })),
    hiddenCount,
    height: canvasHeight(tallest),
    hasContainedProducts: subject.children.size > 0,
    placed: {
      usedBy: [...placed.usedBy.keys()],
      dependsOn: [...placed.dependsOn.keys()],
    },
  }
}
