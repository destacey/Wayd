import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid-core/grid-sorting'
import {
  StatusCategory,
  StatusTransitionDto,
  StatusWorkflowDetailsDto,
  TransitionConditionKind,
  TransitionMode,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import { transitionConditionLabels } from './transition-conditions'
import { statusCategoryFromId } from './status-category'

/** The categories in the order a workflow flows, which is the order its columns are drawn in. */
export const CATEGORY_ORDER: readonly StatusCategory[] = [
  StatusCategory.Proposed,
  StatusCategory.Active,
  StatusCategory.Done,
  StatusCategory.Removed,
]

export const DIAGRAM_NODE_WIDTH = 160
export const DIAGRAM_NODE_HEIGHT = 64
const COLUMN_GAP = 56
/** Wide enough for an action's name on a move between neighbouring columns. */
const LABELLED_COLUMN_GAP = 128
const ROW_GAP = 40
const HEADER_HEIGHT = 40
const COLUMN_PADDING = 12
/** How far the first lane over or under the columns sits from them, and how far apart lanes are. */
const LANE_START = 20
const LANE_GAP = 18
/** How far a move within a column bows out from it, and how far apart such bows are: inside its padding. */
const BOW_START = 6
const BOW_GAP = 4
/**
 * Where a move over or under the columns climbs: just outside the column it leaves or enters, so the
 * middle of the gap stays clear for the labels of moves to the next column.
 */
const GUTTER = COLUMN_PADDING + 6
const GUTTER_STEP = 6
const MAX_GUTTER_LANES = 3
const CORNER_RADIUS = 8

/** One category's column: a backdrop with the category's name over its statuses. */
export interface WorkflowColumn {
  category: StatusCategory
  label: string
  x: number
  width: number
  height: number
}

export interface WorkflowDiagramNode {
  id: string
  name: string
  category: StatusCategory
  /** The status's well-known meaning in the owner type's vocabulary, where it carries one. */
  aliasName?: string
  /** Where a new record starts. */
  isInitial: boolean
  /** The status the record holds, when the diagram is drawn for one. */
  isCurrent: boolean
  /** A status the record has held under this workflow. */
  isVisited: boolean
  /** Position within the diagram, top-left. */
  x: number
  y: number
}

/**
 * How an edge runs, so it never crosses a status it does not join and a pair of opposite moves stay
 * apart:
 * - `forward`: straight across to the next column;
 * - `over`: out sideways, up the gap beside the column, across above every column, and down the gap
 *   beside the target's — to a later column that is not the next;
 * - `backward`: the same below every column, back to an earlier one;
 * - `down` / `up`: within a column, bowing out to its right going down and its left going up.
 */
export type WorkflowEdgeRoute = 'forward' | 'over' | 'backward' | 'down' | 'up'

/**
 * Why an edge is drawn:
 * - `allowed`: the workflow allows the move;
 * - `omitted`: an action makes the move but the workflow leaves it out, so the action is refused;
 * - `taken`: the record made the move although the workflow does not draw it, as under any move.
 */
export type WorkflowEdgeKind = 'allowed' | 'omitted' | 'taken'

export interface WorkflowDiagramEdge {
  id: string
  source: string
  target: string
  kind: WorkflowEdgeKind
  route: WorkflowEdgeRoute
  /** How many columns the move crosses; zero within a column. */
  span: number
  /**
   * Which of its route's lanes the move runs in, so moves whose stretches overlap never share a line.
   * Zero is the lane nearest the statuses.
   */
  lane: number
  /** For `over` and `backward`, the height the move crosses at, above or below every column. */
  laneY?: number
  /** How far from a status's side an `over` or `backward` move climbs, in the gap beside the column. */
  gutter: number
  /** The actions that make the move, as the owner type names them. Empty where none does. */
  actions: string[]
  /** An action depends on the move, so the workflow cannot leave it out. */
  isRequired: boolean
  conditions: TransitionConditionKind[]
  /** The record made this move under this workflow. */
  isTaken: boolean
  /** What hovering the edge says, one line each. */
  summary: string[]
}

export interface WorkflowDiagram {
  columns: WorkflowColumn[]
  nodes: WorkflowDiagramNode[]
  edges: WorkflowDiagramEdge[]
  /** Records may move between any two statuses, so no edge is drawn for the workflow itself. */
  allowsAnyMove: boolean
  /** What the canvas needs to show the whole diagram. */
  width: number
  height: number
  /** How far the moves reach above and below the columns, which start at zero. */
  top: number
  bottom: number
}

/** Where a record has been under the workflow it is drawn on, read from its status history. */
export interface WorkflowRecordPath {
  currentStatusId?: string
  visitedStatusIds: Set<string>
  /** Moves made under this workflow, keyed `from:to`. */
  takenMoves: Set<string>
  /**
   * Changes the diagram cannot draw, newest first: those made under an earlier workflow, and the switch
   * onto this one, whose starting status this workflow does not have.
   */
  notDrawn: StatusTransitionDto[]
}

export interface WorkflowDiagramOptions {
  /** Draws the moves an action makes that the workflow leaves out, faintly. */
  showOmittedMoves?: boolean
  path?: WorkflowRecordPath
}

export const moveKey = (fromStatusId: string, toStatusId: string) =>
  `${fromStatusId}:${toStatusId}`

/**
 * A workflow status's category. An unknown number reads as Proposed rather than dropping the status
 * from the drawing.
 */
export const statusCategoryOf = (status: WorkflowStatusDto): StatusCategory =>
  statusCategoryFromId(status.category.id) ?? StatusCategory.Proposed

const byOrder = (a: WorkflowStatusDto, b: WorkflowStatusDto) =>
  a.order - b.order || caseInsensitiveCompare(a.name, b.name)

/**
 * Where a new record starts: the lowest-ordered Proposed status, or the lowest-ordered status when there
 * is none. Mirrors the workflow engine's own rule.
 */
export const initialStatusId = (
  statuses: readonly WorkflowStatusDto[],
): string | undefined => {
  const ordered = [...statuses].sort(byOrder)

  return (
    ordered.find((s) => statusCategoryOf(s) === StatusCategory.Proposed) ??
    ordered[0]
  )?.id
}

/**
 * Reads a record's status history against the workflow it is on.
 *
 * Only a change made under this workflow, between two of its statuses, can be drawn on it. Everything
 * else — a change under an earlier workflow, and the switch from one onto this — is returned to be
 * listed instead. A record moved away and back has every stay on this workflow drawn, since each was
 * a move this workflow allowed.
 */
export const buildRecordPath = (
  workflow: Pick<StatusWorkflowDetailsDto, 'id' | 'statuses'>,
  history: readonly StatusTransitionDto[],
): WorkflowRecordPath => {
  const statusIds = new Set(workflow.statuses.map((s) => s.id))
  const path: WorkflowRecordPath = {
    visitedStatusIds: new Set(),
    takenMoves: new Set(),
    notDrawn: [],
  }

  const newestFirst = [...history].sort((a, b) => b.sequence - a.sequence)
  const newest = newestFirst[0]
  if (
    newest &&
    newest.workflowId === workflow.id &&
    statusIds.has(newest.toStatus.id)
  ) {
    path.currentStatusId = newest.toStatus.id
  }

  for (const entry of newestFirst) {
    const onThisWorkflow =
      entry.workflowId === workflow.id && statusIds.has(entry.toStatus.id)
    if (onThisWorkflow) {
      path.visitedStatusIds.add(entry.toStatus.id)
    }

    if (!entry.fromStatus) {
      if (!onThisWorkflow) path.notDrawn.push(entry)
      continue
    }

    if (onThisWorkflow && statusIds.has(entry.fromStatus.id)) {
      path.visitedStatusIds.add(entry.fromStatus.id)
      path.takenMoves.add(moveKey(entry.fromStatus.id, entry.toStatus.id))
    } else {
      path.notDrawn.push(entry)
    }
  }

  return path
}

/**
 * Lays a workflow out left to right: one column per category in flow order, holding its statuses in
 * their order. Deterministic, so the same workflow always draws the same way, and empty categories take
 * no room.
 */
export const buildWorkflowDiagram = (
  workflow: StatusWorkflowDetailsDto,
  options: WorkflowDiagramOptions = {},
): WorkflowDiagram => {
  const { showOmittedMoves = false, path } = options
  const statuses = workflow.statuses ?? []
  const allowsAnyMove = workflow.transitionMode === TransitionMode.AnyToAny
  const initialId = initialStatusId(statuses)
  const gap =
    (workflow.declaredMoves ?? []).length > 0 ? LABELLED_COLUMN_GAP : COLUMN_GAP

  const columns: WorkflowColumn[] = []
  const nodes: WorkflowDiagramNode[] = []
  const columnOf = new Map<string, number>()
  const rowOf = new Map<string, number>()
  const nameOf = new Map(statuses.map((s) => [s.id, s.name]))

  let tallest = 0
  for (const category of CATEGORY_ORDER) {
    const inColumn = statuses
      .filter((s) => statusCategoryOf(s) === category)
      .sort(byOrder)
    if (inColumn.length === 0) continue

    const index = columns.length
    const x = index * (DIAGRAM_NODE_WIDTH + 2 * COLUMN_PADDING + gap)
    const height =
      HEADER_HEIGHT +
      inColumn.length * DIAGRAM_NODE_HEIGHT +
      (inColumn.length - 1) * ROW_GAP +
      COLUMN_PADDING
    tallest = Math.max(tallest, height)
    columns.push({
      category,
      // The workflow detail names each status's category as its owner type labels it.
      label: inColumn[0].category.name || category,
      x,
      width: DIAGRAM_NODE_WIDTH + 2 * COLUMN_PADDING,
      height,
    })

    inColumn.forEach((status, row) => {
      columnOf.set(status.id, index)
      rowOf.set(status.id, row)
      nodes.push({
        id: status.id,
        name: status.name,
        category,
        aliasName: status.aliasName ?? undefined,
        isInitial: status.id === initialId,
        isCurrent: status.id === path?.currentStatusId,
        isVisited: path?.visitedStatusIds.has(status.id) ?? false,
        x: x + COLUMN_PADDING,
        y: HEADER_HEIGHT + row * (DIAGRAM_NODE_HEIGHT + ROW_GAP),
      })
    })
  }

  // Every column is drawn to the tallest, so the backdrops line up.
  for (const column of columns) column.height = tallest

  const route = (from: string, to: string): WorkflowEdgeRoute => {
    const a = columnOf.get(from)!
    const b = columnOf.get(to)!
    if (a === b) return rowOf.get(from)! < rowOf.get(to)! ? 'down' : 'up'
    if (a > b) return 'backward'
    return b - a === 1 ? 'forward' : 'over'
  }

  const declared = new Map(
    (workflow.declaredMoves ?? []).map((m) => [
      moveKey(m.fromStatusId, m.toStatusId),
      m,
    ]),
  )
  const declaresMoves = declared.size > 0

  const describe = (
    from: string,
    to: string,
    kind: WorkflowEdgeKind,
    conditions: TransitionConditionKind[],
    isTaken: boolean,
  ): string[] => {
    const lines = [`${nameOf.get(from)} → ${nameOf.get(to)}`]
    const move = declared.get(moveKey(from, to))
    const actions = move?.actions.join(', ')

    if (kind === 'omitted') {
      lines.push(`Not allowed: ${actions} is refused from ${nameOf.get(from)}`)
    } else if (move) {
      lines.push(
        move.isRequired
          ? `Required: ${actions} makes this move`
          : `Optional: ${actions} makes this move`,
      )
    } else if (declaresMoves && kind === 'allowed') {
      lines.push('No action makes this move')
    }

    for (const condition of conditions) {
      lines.push(transitionConditionLabels[condition])
    }

    if (isTaken) lines.push('This record has made this move')

    return lines
  }

  const edge = (
    from: string,
    to: string,
    kind: WorkflowEdgeKind,
    conditions: TransitionConditionKind[],
  ): WorkflowDiagramEdge => {
    const move = declared.get(moveKey(from, to))
    const isTaken =
      kind !== 'omitted' && !!path?.takenMoves.has(moveKey(from, to))

    return {
      id: `${kind}:${moveKey(from, to)}`,
      source: from,
      target: to,
      kind,
      route: route(from, to),
      span: Math.abs(columnOf.get(to)! - columnOf.get(from)!),
      lane: 0,
      gutter: GUTTER,
      actions: [...(move?.actions ?? [])],
      isRequired: kind === 'allowed' && move?.isRequired === true,
      conditions,
      isTaken,
      summary: describe(from, to, kind, conditions, isTaken),
    }
  }

  const drawable = (from: string, to: string) =>
    columnOf.has(from) && columnOf.has(to) && from !== to

  const edges: WorkflowDiagramEdge[] = []
  const drawn = new Set<string>()

  if (!allowsAnyMove) {
    for (const transition of workflow.transitions ?? []) {
      const key = moveKey(transition.fromStatusId, transition.toStatusId)
      if (
        drawn.has(key) ||
        !drawable(transition.fromStatusId, transition.toStatusId)
      )
        continue
      drawn.add(key)
      edges.push(
        edge(transition.fromStatusId, transition.toStatusId, 'allowed', [
          ...(transition.conditions ?? []),
        ]),
      )
    }

    if (showOmittedMoves) {
      for (const [key, move] of declared) {
        if (drawn.has(key) || !drawable(move.fromStatusId, move.toStatusId))
          continue
        drawn.add(key)
        edges.push(edge(move.fromStatusId, move.toStatusId, 'omitted', []))
      }
    }
  }

  // A move the record made that the workflow does not draw: every move under any-move, or one a
  // restricted workflow has since left out. It happened, so the path shows it.
  for (const key of path?.takenMoves ?? []) {
    if (drawn.has(key)) continue
    const [from, to] = key.split(':')
    if (!drawable(from, to)) continue
    drawn.add(key)
    edges.push(edge(from, to, 'taken', []))
  }

  // Each move takes the lowest lane its stretch does not share with another of its group: moves over or
  // under the columns by the columns they span, moves within a column by the rows they span.
  const stretchOf = (e: WorkflowDiagramEdge): [number, number] => {
    const [a, b] =
      e.route === 'down' || e.route === 'up'
        ? [rowOf.get(e.source)!, rowOf.get(e.target)!]
        : [columnOf.get(e.source)!, columnOf.get(e.target)!]
    return [Math.min(a, b), Math.max(a, b)]
  }
  const groupOf = (e: WorkflowDiagramEdge) =>
    e.route === 'down' || e.route === 'up'
      ? `${e.route}:${columnOf.get(e.source)}`
      : e.route
  const groups = new Map<string, WorkflowDiagramEdge[]>()
  for (const e of edges) {
    if (e.route === 'forward') continue
    groups.set(groupOf(e), [...(groups.get(groupOf(e)) ?? []), e])
  }
  for (const group of groups.values()) {
    const lanes: [number, number][][] = []
    const byLength = [...group].sort((a, b) => {
      const [a0, a1] = stretchOf(a)
      const [b0, b1] = stretchOf(b)
      return a1 - a0 - (b1 - b0) || a0 - b0
    })
    for (const e of byLength) {
      const [start, end] = stretchOf(e)
      let lane = lanes.findIndex((taken) =>
        taken.every(([s, t]) => end <= s || t <= start),
      )
      if (lane === -1) lane = lanes.push([]) - 1
      lanes[lane].push([start, end])
      e.lane = lane
    }
  }

  for (const e of edges) {
    if (e.route === 'over') e.laneY = -(LANE_START + e.lane * LANE_GAP)
    if (e.route === 'backward')
      e.laneY = tallest + LANE_START + e.lane * LANE_GAP
  }

  const laneYs = edges.flatMap((e) => (e.laneY === undefined ? [] : [e.laneY]))
  const last = columns.at(-1)

  return {
    columns,
    nodes,
    edges,
    allowsAnyMove,
    width: last ? last.x + last.width : 0,
    height: tallest,
    top: Math.min(0, ...laneYs),
    bottom: Math.max(tallest, ...laneYs),
  }
}

type Point = { x: number; y: number }

/**
 * A path through `points` with its corners rounded, as SVG. A corner's radius shrinks to fit a short
 * stretch, so a tight bend never overshoots.
 */
export const roundedPath = (points: readonly Point[]): string => {
  const [first, ...rest] = points
  let d = `M ${first.x},${first.y}`

  for (let i = 0; i < rest.length - 1; i++) {
    const previous = i === 0 ? first : rest[i - 1]
    const corner = rest[i]
    const next = rest[i + 1]
    const before = Math.hypot(corner.x - previous.x, corner.y - previous.y)
    const after = Math.hypot(next.x - corner.x, next.y - corner.y)
    const r = Math.min(CORNER_RADIUS, before / 2, after / 2)
    const toward = (from: Point, length: number) => ({
      x: corner.x + ((from.x - corner.x) / (length || 1)) * r,
      y: corner.y + ((from.y - corner.y) / (length || 1)) * r,
    })
    const start = toward(previous, before)
    const end = toward(next, after)
    d += ` L ${start.x},${start.y} Q ${corner.x},${corner.y} ${end.x},${end.y}`
  }

  const end = rest.at(-1) ?? first
  return `${d} L ${end.x},${end.y}`
}

/**
 * The path a routed move takes between the handles React Flow places, with the point its label sits
 * at. Null for a `forward` move, which is a plain curve across the gap.
 *
 * `over` and `backward` leave and enter sideways and travel the gaps between columns and the lanes
 * above or below them, where no status is; a move within a column bows out beside it. Either way the
 * line never runs through a status it does not join.
 */
export const edgePath = (
  edge: Pick<WorkflowDiagramEdge, 'route' | 'lane' | 'laneY' | 'gutter'>,
  source: Point,
  target: Point,
): [path: string, labelX: number, labelY: number] | null => {
  // Lanes past the last that fits share it rather than reaching across into the next column's labels.
  const offset = Math.min(edge.lane, MAX_GUTTER_LANES) * GUTTER_STEP

  switch (edge.route) {
    case 'forward':
      return null
    case 'over':
    case 'backward': {
      const laneY = edge.laneY!
      // Over leaves a status's right side and enters the target's left; backward the reverse.
      const out = edge.route === 'over' ? 1 : -1
      const sourceX = source.x + out * (edge.gutter + offset)
      const targetX = target.x - out * (edge.gutter + offset)
      return [
        roundedPath([
          source,
          { x: sourceX, y: source.y },
          { x: sourceX, y: laneY },
          { x: targetX, y: laneY },
          { x: targetX, y: target.y },
          target,
        ]),
        (sourceX + targetX) / 2,
        laneY,
      ]
    }
    case 'down':
    case 'up': {
      const out = edge.route === 'down' ? 1 : -1
      const x = source.x + out * (BOW_START + edge.lane * BOW_GAP)
      return [
        roundedPath([source, { x, y: source.y }, { x, y: target.y }, target]),
        x,
        (source.y + target.y) / 2,
      ]
    }
  }
}

/** What the diagram says when records may move freely, naming the owner type where its actions decide. */
export const anyMoveNote = (
  workflow: Pick<StatusWorkflowDetailsDto, 'owner' | 'declaredMoves'>,
) =>
  (workflow.declaredMoves ?? []).length > 0
    ? `Any move is allowed. ${workflow.owner.name} records still move only through their actions.`
    : 'Any move is allowed: a record can move from any status to any other.'
