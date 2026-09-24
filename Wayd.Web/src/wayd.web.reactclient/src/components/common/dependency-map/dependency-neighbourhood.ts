import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid-core/grid-sorting'
import type { Edge, Node } from '@xyflow/react'

/**
 * Which column a node sits in, relative to the record the map is centred on. Every arrow runs left to
 * right: the left side holds the records whose links arrive at the subject, the right side those the
 * subject's links arrive at. What an arrow means (relies on, must finish before) is the caller's.
 */
export type DependencyNodeSide = 'left' | 'center' | 'right'

/** The two sides a map grows along. */
export type DependencyFarSide = Exclude<DependencyNodeSide, 'center'>

/** A record at one end of a link, or one that such a record sits inside. */
export interface DependencyMapRecord {
  id: string
  label: string
  /** Where the node, or the box it names, links to. Absent draws it as plain text. */
  href?: string
}

/** One link, as the map draws it. */
export interface DependencyMapLink {
  id: string
  /** The end the arrow leaves. */
  source: DependencyMapRecord
  /** The end the arrow points at. */
  target: DependencyMapRecord
  /** What the source sits inside, outermost first. Each step is drawn as a box around it. */
  sourcePath: DependencyMapRecord[]
  targetPath: DependencyMapRecord[]
  /** Which of the caller's edge styles the link is drawn in. */
  variant: string
}

/**
 * A record's links, split by the side of the map they grow: `left` holds those the record is the target
 * of, `right` those it is the source of.
 */
export interface DependencyMapLinks {
  left: DependencyMapLink[]
  right: DependencyMapLink[]
}

/**
 * Where a record off to one side stands with its own links: not asked for, being fetched, drawn, found
 * to have nothing further out, or failed to load.
 */
export type DependencyExpansionStatus =
  'collapsed' | 'loading' | 'expanded' | 'empty' | 'error'

export interface DependencyNodeData extends Record<string, unknown> {
  label: string
  side: DependencyNodeSide
  /** The record's id, which is what an expansion is keyed by. */
  recordId: string
  href?: string
  /** Set on the record the map is centred on, which reads as the subject rather than a neighbour. */
  isSubject?: boolean
  /** Set on every record off to one side, which can be expanded; never on the centre column. */
  expansion?: DependencyExpansionStatus
}

export interface DependencyGroupData extends Record<string, unknown> {
  label: string
  href?: string
}

/** The count of records an expansion, or the subject, left off a column. */
export interface DependencyOverflowData extends Record<string, unknown> {
  label: string
  side: DependencyFarSide
  /** The record whose links overflowed, or null for the subject's own. */
  ownerId: string | null
  count: number
}

export interface DependencyEdgeData extends Record<string, unknown> {
  variant: string
}

export type DependencyNode = Node<
  DependencyNodeData | DependencyGroupData | DependencyOverflowData
>
export type DependencyEdge = Edge<DependencyEdgeData>

/** A record the reader expanded, with whatever has loaded for it. */
export interface DependencyExpansion {
  /** Undefined while loading. Already filtered to what the map should draw. */
  links?: DependencyMapLinks
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
   * leaves a record with two links sitting in an empty field.
   */
  height: number
  /**
   * Whether the subject is drawn as a box holding the records beneath it, which the caption explains
   * only when it happens. Boxes on the far sides are what those records sit inside, which the caption's
   * sentence does not describe.
   */
  hasContainedRecords: boolean
  /** The records drawn on each side, which is what an expansion must still reach to stay open. */
  placed: Record<DependencyFarSide, string[]>
}

export interface BuildDependencyNeighbourhoodOptions {
  subject: DependencyMapRecord
  /** The subject's links, already filtered to what the map should draw. */
  links: DependencyMapLinks | undefined
  /** Records drawn per column, per record whose links fill it, before the rest collapse into a count. */
  maxPerSide?: number
  /**
   * A word the overflow counts carry while a filter is on ("+3 more hard"), so a count is not read as
   * everything that was left off.
   */
  overflowQualifier?: string
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

const FAR_SIDES: DependencyFarSide[] = ['right', 'left']

/**
 * Node ids are scoped by side, because the same record can be on both sides of a map: a record the
 * subject's links reach may also reach the subject. Sharing one node would drag a box from one side into
 * the other's column, where an arrow pointing back the way it came is the only clue that a record sits on
 * the wrong side. Within a side a record is drawn once, however many columns reach it.
 */
export const sideNodeId = (side: DependencyNodeSide, recordId: string) =>
  side === 'center' ? recordId : `${side}:${recordId}`

/**
 * Boxes are scoped by column as well as side: a box can hold one record found in the first column and
 * another found two columns out, and each column draws its own box.
 */
export const groupNodeId = (
  side: DependencyNodeSide,
  recordId: string,
  column = 1,
) =>
  column === 1
    ? `group:${sideNodeId(side, recordId)}`
    : `group:${side}:${column}:${recordId}`

export const overflowNodeId = (
  side: DependencyFarSide,
  ownerId: string | null,
) => `overflow:${side}:${ownerId ?? 'subject'}`

/**
 * Tall enough for the graph, within bounds: below the floor it is cramped, above it the map is more page
 * than a reader wants to scroll past. Nesting makes graphs much taller than a flat list, so a low
 * ceiling would zoom a large record's map down to unreadable. The canvas is capped again in CSS
 * against the viewport, which this cannot know.
 */
const canvasHeight = (contentHeight: number) =>
  Math.min(
    MAX_CANVAS_HEIGHT,
    Math.max(MIN_CANVAS_HEIGHT, contentHeight + CANVAS_MARGIN),
  )

/**
 * A record on the map, with whatever of its own records are also on the map beneath it.
 *
 * `holdsLink` and children are independent: a record can both hold a dependency of its own and contain
 * another that holds one, which is drawn as a box with the record's own node inside it.
 */
interface TreeNode {
  id: string
  label: string
  href?: string
  holdsLink: boolean
  isSubject: boolean
  expansion?: DependencyExpansionStatus
  children: Map<string, TreeNode>
}

const emptyNode = (
  record: DependencyMapRecord,
  isSubject = false,
): TreeNode => ({
  id: record.id,
  label: record.label,
  href: record.href,
  holdsLink: false,
  isSubject,
  children: new Map(),
})

/**
 * Files a record under the chain of records it sits inside, creating the boxes on the way down, and
 * returns the record's own node.
 */
const insert = (
  root: TreeNode,
  path: DependencyMapRecord[],
  record: DependencyMapRecord,
): TreeNode => {
  let node = root

  for (const step of path) {
    node = node.children.get(step.id) ?? add(node, step)
  }

  if (record.id === root.id) {
    root.holdsLink = true
    return root
  }

  const own = add(node, record)
  own.holdsLink = true

  return own
}

const add = (parent: TreeNode, record: DependencyMapRecord): TreeNode => {
  const existing = parent.children.get(record.id)
  if (existing) return existing

  const child = emptyNode(record)
  parent.children.set(record.id, child)

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
 * How much room a record needs: a leaf is one node, a box is its header plus everything inside it.
 *
 * Measured before anything is placed, because a box cannot be positioned until its size is known and its
 * size depends on its deepest child.
 */
const measure = (node: TreeNode): Size => {
  const children = childrenInOrder(node)
  if (children.length === 0) {
    return { width: LEAF_WIDTH, height: LEAF_HEIGHT }
  }

  // The record's own node sits inside its box, above the records it contains.
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

/** The records a tree draws as nodes, top to bottom, which is the order a column reads in. */
const recordsInOrder = (node: TreeNode): string[] => [
  ...(node.holdsLink ? [node.id] : []),
  ...childrenInOrder(node).flatMap(recordsInOrder),
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

  const recordNode = (
    at: { x: number; y: number },
    parent: string | undefined,
  ): DependencyNode => ({
    id: sideNodeId(side, node.id),
    type: 'record',
    position: at,
    ...(parent ? { parentId: parent, extent: 'parent' as const } : {}),
    data: {
      label: node.label,
      side,
      recordId: node.id,
      ...(node.href ? { href: node.href } : {}),
      ...(node.isSubject ? { isSubject: true } : {}),
      ...(node.expansion ? { expansion: node.expansion } : {}),
    },
  })

  if (children.length === 0) {
    into.push(recordNode({ x, y }, parentId))
    return
  }

  const size = measure(node)
  const groupId = groupNodeId(side, node.id, column)

  into.push({
    id: groupId,
    type: 'recordGroup',
    position: { x, y },
    // Sized through width/height rather than style: both render the same box, but only these are read
    // back off the node, and the image export sizes the box from what it reads.
    width: size.width,
    height: size.height,
    ...(parentId ? { parentId, extent: 'parent' as const } : {}),
    data: {
      label: node.label,
      ...(node.href ? { href: node.href } : {}),
    },
  })

  let offsetY = GROUP_HEADER

  if (node.holdsLink) {
    into.push(recordNode({ x: GROUP_PADDING_X, y: offsetY }, groupId))
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

/** One column on one side: the records it found, boxed by what they belong to, and what it left off. */
interface Column {
  roots: Map<string, { root: TreeNode; rank: number; order: number }>
  overflows: Overflow[]
}

/** Stacks a column's records, and reports how tall and wide the column ended up. */
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

/** The connection points on every record node, which an edge names so it joins the sides it should. */
export const DEPENDENCY_HANDLES = {
  inLeft: 'in-left',
  outRight: 'out-right',
  outLeft: 'out-left',
  inRight: 'in-right',
} as const

/**
 * Which sides of its two nodes an edge joins, from the columns they sit in.
 *
 * Every link the subject holds, and every record an expansion finds, runs left to right. A record an
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
 * Beside whatever they were expanded from, then in the order the links arrived. Keeping each expansion's records near their source is what stops every edge from crossing the
 * column.
 */
const orderedRoots = (column: Column) =>
  [...column.roots.values()]
    .sort((a, b) => a.rank - b.rank || a.order - b.order)
    .map((entry) => entry.root)

/**
 * The map of a record's links in both directions: one hop out, and further along any record the reader
 * expanded. The caller filters the links and decides what they mean; this decides where they go.
 *
 * Every record is drawn inside what its path says contains it, on both sides: a link rolled up from a
 * descendant of the subject starts at that descendant, nested under whatever it belongs to, and a far
 * record is drawn inside its own path's boxes. Containment is a box rather than an edge, because an edge
 * would put "is part of" and "is linked to" in the same visual language — and crediting a parent with its
 * child's link would claim a link the parent does not hold.
 *
 * An expansion only ever grows outward, along the side its record is on. It draws only the links that
 * record holds itself, not ones rolled up from beneath it, which are left for its own map. A record
 * reached again on the same side keeps the column it was first found in, so expanding one record never
 * moves another.
 */
export const buildDependencyNeighbourhood = ({
  subject: subjectRecord,
  links: subjectLinks,
  maxPerSide = 6,
  overflowQualifier,
  expansions,
  subjectShowAll = [],
}: BuildDependencyNeighbourhoodOptions): DependencyNeighbourhood => {
  const subjectId = subjectRecord.id
  const subject = emptyNode(subjectRecord, true)

  const edges = new Map<string, DependencyEdge>()
  const columns: Record<DependencyFarSide, Column[]> = {
    left: [],
    right: [],
  }
  const placed: Record<DependencyFarSide, Map<string, TreeNode>> = {
    left: new Map(),
    right: new Map(),
  }
  // Signed, left of the subject negative: which way an edge runs decides which sides of its nodes it
  // joins. Anything not in here is in the centre column.
  const columnOf = new Map<string, number>()
  let hiddenCount = 0

  const overflowLabel = (count: number, owner: string | null) =>
    `+${count} more${overflowQualifier ? ` ${overflowQualifier}` : ''}${owner ? ` for ${owner}` : ''}`

  for (const side of FAR_SIDES) {
    const ends = (link: DependencyMapLink) =>
      side === 'right'
        ? {
            far: link.target,
            farPath: link.targetPath,
            near: link.source,
            nearPath: link.sourcePath,
          }
        : {
            far: link.source,
            farPath: link.sourcePath,
            near: link.target,
            nearPath: link.targetPath,
          }

    const columnAt = (number: number) =>
      (columns[side][number - 1] ??= { roots: new Map(), overflows: [] })

    /**
     * Draws one record's links into the column beyond it. `owner` is null for the subject, whose links
     * start inside the centre box rather than at a node already on this side.
     */
    const grow = (
      owner: TreeNode | null,
      links: DependencyMapLink[],
      columnNumber: number,
      rank: number,
      showAll: boolean,
    ) => {
      // Grouped before the cap, so it limits records drawn rather than links: cutting mid-record would
      // show a node missing the very edges that explain it.
      const byFarEnd = new Map<string, DependencyMapLink[]>()
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
            // The near end's chain is trimmed to the part inside this record; everything above it is
            // the record's own ancestry, which the map is not about. A link the record holds itself
            // carries the record's own ancestry and nothing below it — and a record is not in its own
            // path, so an untrimmed chain would build the record's parents inside its box, upside down.
            const depth = nearPath.findIndex((step) => step.id === subjectId)
            const inside = depth === -1 ? [] : nearPath.slice(depth + 1)
            insert(subject, inside, near)
          }

          if (!placed[side].has(far.id)) {
            const rootRecord = farPath[0] ?? far
            const existing = column.roots.get(rootRecord.id)
            const entry = existing ?? {
              root: emptyNode(rootRecord),
              rank,
              order: column.roots.size,
            }
            entry.rank = Math.min(entry.rank, rank)
            column.roots.set(rootRecord.id, entry)
            placed[side].set(far.id, insert(entry.root, farPath.slice(1), far))
            columnOf.set(
              sideNodeId(side, far.id),
              side === 'right' ? columnNumber : -columnNumber,
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
            source: side === 'right' ? nearId : farId,
            target: side === 'right' ? farId : nearId,
            data: { variant: link.variant },
          })
        }
      }
    }

    grow(null, subjectLinks?.[side] ?? [], 1, 0, subjectShowAll.includes(side))

    // Column by column, so each expansion knows where its source sits before its own records are
    // ordered. A record placed further out by an earlier expansion is still expanded in its own turn.
    for (let number = 1; number <= columns[side].length; number++) {
      const order = orderedRoots(columns[side][number - 1]).flatMap(
        recordsInOrder,
      )

      order.forEach((id, rank) => {
        const node = placed[side].get(id)
        // A record first found in an earlier column is expanded there, not again here.
        if (!node || node.expansion) return

        const expansion = expansions?.[side][id]
        if (!expansion) {
          node.expansion = 'collapsed'
          return
        }
        if (!expansion.links) {
          node.expansion = expansion.isError ? 'error' : 'loading'
          return
        }

        const isInsideSubject = (
          end: DependencyMapRecord,
          path: DependencyMapRecord[],
        ) => end.id === subjectId || path.some((step) => step.id === subjectId)

        const links = expansion.links[side].filter((link) => {
          const { far, farPath, near } = ends(link)
          // Only what the record holds itself, and never back into the subject: every link touching
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
      hasContainedRecords: false,
      placed: { left: [], right: [] },
    }
  }

  const nodes: DependencyNode[] = []

  const stacks = (side: DependencyFarSide) =>
    columns[side].map((column, index) => stack(column, side, index + 1, nodes))

  // Left to right: the furthest left column first.
  const left = stacks('left').reverse()
  const centre = stack(
    {
      roots: new Map([[subjectId, { root: subject, rank: 0, order: 0 }]]),
      overflows: [],
    },
    'center',
    1,
    nodes,
  )
  const right = stacks('right')
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
    hasContainedRecords: subject.children.size > 0,
    placed: {
      left: [...placed.left.keys()],
      right: [...placed.right.keys()],
    },
  }
}
