import {
  DependencyStrength,
  ProductDependenciesDto,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid-core/grid-sorting'
import type { Edge, Node } from '@xyflow/react'

/** Where a node sits relative to the product the map is centred on. */
export type DependencyNodeSide = 'usedBy' | 'center' | 'dependsOn'

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
  /** Whether a descendant holds one of the links, which the caption explains only when it happens. */
  hasContainedProducts: boolean
}

export interface BuildDependencyNeighbourhoodOptions {
  productId: string
  productName: string
  productKey: number
  dependencies: ProductDependenciesDto | undefined
  /** Products drawn per side before the rest collapse into a count. */
  maxPerSide?: number
}

/** Laid out rather than simulated: one hop each way is three columns, and a fixed grid never jitters. */
const COLUMN_X = { usedBy: 0, center: 320, dependsOn: 640 }
const ROW_HEIGHT = 76
const NODE_WIDTH = 180
const GROUP_PADDING_X = 12
const GROUP_HEADER = 34
const GROUP_PADDING_BOTTOM = 12

const MIN_CANVAS_HEIGHT = 180
const MAX_CANVAS_HEIGHT = 420
const CANVAS_MARGIN = 48

/** Tall enough for the graph, within bounds: below the floor it is cramped, above it the card takes the page. */
const canvasHeight = (contentHeight: number) =>
  Math.min(
    MAX_CANVAS_HEIGHT,
    Math.max(MIN_CANVAS_HEIGHT, contentHeight + CANVAS_MARGIN),
  )

export const groupNodeId = (productId: string) => `group:${productId}`

/**
 * The map of what a product relies on and what relies on it, one hop out.
 *
 * Ended links are left out whatever the caller passed: a map reads as what is true now, and a link that
 * has stopped says nothing about today's blast radius. The Dependencies section is where history is read.
 *
 * A neighbour appears once however many links reach it, so a parent whose three services depend on the
 * same provider draws one provider node, and a cycle draws one node with an edge each way.
 *
 * Where a link was recorded against a descendant rather than this product, the descendant is drawn as its
 * own node inside a box for this product. Containment is not a dependency, so it is a box rather than
 * another edge — and the link then starts where it was actually recorded rather than being credited to
 * the parent.
 */
export const buildDependencyNeighbourhood = ({
  productId,
  productName,
  productKey,
  dependencies,
  maxPerSide = 6,
}: BuildDependencyNeighbourhoodOptions): DependencyNeighbourhood => {
  const open = (links: ProductDependencyDto[] | undefined) =>
    (links ?? []).filter((d) => !d.endsOn)

  const sides = [
    { side: 'dependsOn' as const, links: open(dependencies?.dependsOn) },
    { side: 'usedBy' as const, links: open(dependencies?.usedBy) },
  ]

  const farNodes: DependencyNode[] = []
  const edges: DependencyEdge[] = []
  const placed = new Set<string>()
  /** The near ends links were actually recorded against: this product, some of its descendants, or both. */
  const nearEnds = new Map<string, { name: string; key: number }>()
  let hiddenCount = 0

  for (const { side, links } of sides) {
    const farEnd = (link: ProductDependencyDto) =>
      side === 'dependsOn' ? link.dependsOnProduct : link.product
    const nearEnd = (link: ProductDependencyDto) =>
      side === 'dependsOn' ? link.product : link.dependsOnProduct

    // Grouped before the cap, so it limits products drawn rather than links: cutting mid-product would
    // show a node missing the very edges that explain it.
    const byFarEnd = new Map<string, ProductDependencyDto[]>()
    for (const link of links) {
      const id = farEnd(link).id
      byFarEnd.set(id, [...(byFarEnd.get(id) ?? []), link])
    }

    const shown = [...byFarEnd.entries()].slice(0, maxPerSide)
    hiddenCount += [...byFarEnd.values()]
      .slice(maxPerSide)
      .reduce((total, group) => total + group.length, 0)

    shown.forEach(([farEndId, group], index) => {
      const far = farEnd(group[0])

      if (!placed.has(farEndId)) {
        placed.add(farEndId)
        farNodes.push({
          id: farEndId,
          type: 'product',
          position: { x: COLUMN_X[side], y: index * ROW_HEIGHT },
          data: { label: far.name, side, productKey: far.key },
        })
      }

      for (const link of group) {
        const near = nearEnd(link)
        nearEnds.set(near.id, { name: near.name, key: near.key })

        edges.push({
          id: link.id,
          source: side === 'dependsOn' ? near.id : farEndId,
          target: side === 'dependsOn' ? farEndId : near.id,
          data: { strength: link.strength },
        })
      }
    })
  }

  if (farNodes.length === 0) {
    return {
      nodes: [],
      edges: [],
      hiddenCount: 0,
      height: 0,
      hasContainedProducts: false,
    }
  }

  // The product first, then its descendants by name, so the subject does not move as links are added.
  const descendants = [...nearEnds.entries()]
    .filter(([id]) => id !== productId)
    .sort(([, a], [, b]) => caseInsensitiveCompare(a.name, b.name))

  const farRows = Math.max(
    farNodes.filter((n) => n.data.side === 'dependsOn').length,
    farNodes.filter((n) => n.data.side === 'usedBy').length,
  )

  if (descendants.length === 0) {
    // Nothing is rolled up, so a box would enclose the subject and nothing else.
    return {
      nodes: [
        {
          id: productId,
          type: 'product',
          position: {
            x: COLUMN_X.center,
            y: ((farRows - 1) / 2) * ROW_HEIGHT,
          },
          data: {
            label: productName,
            side: 'center',
            productKey,
            isSubject: true,
          },
        },
        ...farNodes,
      ],
      edges,
      hiddenCount,
      height: canvasHeight(farRows * ROW_HEIGHT),
      hasContainedProducts: false,
    }
  }

  const members = [
    ...(nearEnds.has(productId)
      ? [[productId, { name: productName, key: productKey }] as const]
      : []),
    ...descendants,
  ]

  const groupHeight =
    GROUP_HEADER + members.length * ROW_HEIGHT + GROUP_PADDING_BOTTOM
  const groupY = Math.max(0, (farRows * ROW_HEIGHT - groupHeight) / 2)

  const group: DependencyNode = {
    id: groupNodeId(productId),
    type: 'productGroup',
    position: { x: COLUMN_X.center - GROUP_PADDING_X, y: groupY },
    style: { width: NODE_WIDTH + GROUP_PADDING_X * 2, height: groupHeight },
    data: { label: productName, productKey },
  }

  const memberNodes: DependencyNode[] = members.map(([id, near], index) => ({
    id,
    type: 'product',
    // Positions inside a group are relative to it.
    position: { x: GROUP_PADDING_X, y: GROUP_HEADER + index * ROW_HEIGHT },
    parentId: group.id,
    extent: 'parent',
    data: {
      label: near.name,
      side: 'center',
      productKey: near.key,
      isSubject: id === productId,
    },
  }))

  // The group is listed before its members, which React Flow requires.
  return {
    nodes: [group, ...memberNodes, ...farNodes],
    edges,
    hiddenCount,
    height: canvasHeight(Math.max(farRows * ROW_HEIGHT, groupHeight)),
    hasContainedProducts: true,
  }
}
