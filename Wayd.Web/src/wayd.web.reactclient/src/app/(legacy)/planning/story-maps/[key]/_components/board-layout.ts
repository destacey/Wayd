import {
  StoryMapDetailsDto,
  StoryMapGoalDto,
  StoryMapStepDto,
  StoryMapSwimLaneDto,
  StoryMapTaskDto,
} from '@/src/services/wayd-api'

/**
 * The board renders as one CSS grid so every goal, step, and task cell lines up across the whole
 * map. A step is the unit of column: the grid has one column track per step (plus the leading label
 * column), a goal header spans the tracks of its own steps, and each task cell is the intersection
 * of a step column and a swim-lane row.
 *
 * These helpers flatten the nested `map.goals[].steps[].tasks[]` graph into that column/row
 * geometry once, so the cell components only place themselves and never recompute spans.
 */

/** Grid line numbers are 1-based, and column 1 is the sticky label column. */
export const LABEL_COLUMN = 1
const FIRST_STEP_COLUMN = 2

/** Shared empty set, so the default collapse state allocates nothing per call. */
const EMPTY_IDS: ReadonlySet<string> = new Set()

/**
 * Grid row lines: goals, then steps, then the swim lanes. An expanded lane takes two rows — its
 * header banner, then its row of task cells — while a collapsed one takes only the banner.
 */
export const GOAL_ROW = 1
export const STEP_ROW = 2
const FIRST_SWIM_LANE_ROW = 3

export interface StepPlacement {
  step: StoryMapStepDto
  goalId: string
  /** 1-based grid column line this step's cell occupies. */
  column: number
  /** Position within its own goal, used as the reorder target index. */
  indexInGoal: number
}

export interface GoalPlacement {
  goal: StoryMapGoalDto
  /** 1-based grid column line where the goal header starts. */
  columnStart: number
  /** Number of column tracks the header spans — one per step, minimum 1. */
  columnSpan: number
  /** Position among goals, used as the reorder target index. */
  index: number
  /** A step-less goal still claims one track, which needs blank filler cells in the rows below. */
  isPlaceholderColumn: boolean
  /** Folds to a single narrow track its header runs down; contributes no step columns. */
  isCollapsed: boolean
}

export interface SwimLanePlacement {
  swimLane: StoryMapSwimLaneDto
  /** 1-based grid row line of the lane's full-width header banner. */
  headerRow: number
  /**
   * 1-based grid row line this lane's task cells occupy, directly under the header — null when the
   * lane is collapsed and claims no task row.
   */
  row: number | null
  index: number
  /** Renders as the banner alone. */
  isCollapsed: boolean
}

export interface BoardLayout {
  goals: GoalPlacement[]
  steps: StepPlacement[]
  swimLanes: SwimLanePlacement[]
  /** 1-based line number of the right-most column, used to spot outer-edge cells. */
  lastColumn: number
  /**
   * One entry per step track, in column order, each the CSS width for that track. NOT derivable from
   * `steps.length` — a step-less goal claims a placeholder track and a collapsed goal a narrow
   * spine, and declaring too few tracks makes the browser size the overflow ones by content instead
   * of `1fr`, so goals stop sharing width equally.
   */
  stepColumnTracks: string[]
  /** Track counts by kind, so the board can floor its width at the sum of the minimums. */
  flexibleColumnCount: number
  collapsedColumnCount: number
  /** Tasks keyed by `${stepId}:${swimLaneId}`, each list sorted by order. */
  tasksByCell: Map<string, StoryMapTaskDto[]>
}

/** Key for a task cell — the intersection of a step column and a swim-lane row. */
export const cellKey = (stepId: string, swimLaneId: string) =>
  `${stepId}:${swimLaneId}`

const byOrder = <T extends { order: number }>(items: T[]): T[] =>
  [...items].sort((a, b) => a.order - b.order)

/** What the viewer has folded away. Transient view state — see the note on `buildBoardLayout`. */
export interface BoardCollapseState {
  goalIds?: ReadonlySet<string>
  swimLaneIds?: ReadonlySet<string>
}

const NO_COLLAPSE: BoardCollapseState = {}

/**
 * Walk the map in display order, assigning each step the next column and each goal a header span
 * covering its steps. A step-less goal still claims one column so its header stays reachable.
 *
 * A collapsed node renders no cells, so anything it hides is left out of `tasksByCell` — a cell that
 * renders nothing must not be a drop target.
 */
export const buildBoardLayout = (
  map: StoryMapDetailsDto,
  collapsed: BoardCollapseState = NO_COLLAPSE,
): BoardLayout => {
  const collapsedGoalIds = collapsed.goalIds ?? EMPTY_IDS
  const collapsedSwimLaneIds = collapsed.swimLaneIds ?? EMPTY_IDS

  const goals: GoalPlacement[] = []
  const steps: StepPlacement[] = []

  let column = FIRST_STEP_COLUMN

  const collapsedColumns = new Set<number>()

  byOrder(map.goals).forEach((goal, index) => {
    const goalSteps = byOrder(goal.steps)
    const isCollapsed = collapsedGoalIds.has(goal.id)
    const columnStart = column

    if (!isCollapsed) {
      goalSteps.forEach((step, indexInGoal) => {
        steps.push({ step, goalId: goal.id, column, indexInGoal })
        column += 1
      })
    }

    // Both fold to a single track, but the placeholder accepts a dropped step and the spine does not.
    const isPlaceholderColumn = !isCollapsed && goalSteps.length === 0
    const columnSpan = isCollapsed ? 1 : Math.max(goalSteps.length, 1)
    if (isCollapsed) collapsedColumns.add(column)
    if (isCollapsed || isPlaceholderColumn) column += 1

    goals.push({
      goal,
      columnStart,
      columnSpan,
      index,
      isPlaceholderColumn,
      isCollapsed,
    })
  })

  // Rows are accumulated, not derived from the index: an expanded lane takes two (banner, tasks)
  // and a collapsed one takes only its banner.
  let laneRow = FIRST_SWIM_LANE_ROW
  const swimLanes = byOrder(map.swimLanes).map((swimLane, index) => {
    const isCollapsed = collapsedSwimLaneIds.has(swimLane.id)
    const headerRow = laneRow
    const row = isCollapsed ? null : headerRow + 1
    laneRow += isCollapsed ? 1 : 2

    return { swimLane, headerRow, row, index, isCollapsed }
  })

  const tasksByCell = new Map<string, StoryMapTaskDto[]>()
  for (const { step } of steps) {
    for (const task of step.tasks) {
      // Drop resolution counts siblings in this map, so a hidden cell must not appear in it. A
      // collapsed goal needs no equivalent check — it contributes no steps, so the loop above never
      // reaches its tasks.
      if (collapsedSwimLaneIds.has(task.swimLaneId)) continue

      const key = cellKey(step.id, task.swimLaneId)
      const list = tasksByCell.get(key)
      if (list) list.push(task)
      else tasksByCell.set(key, [task])
    }
  }
  for (const list of tasksByCell.values()) {
    list.sort((a, b) => a.order - b.order)
  }

  const lastColumn = column - 1

  // Floored at 1 so a map with no goals still has a track for the grid template to declare.
  const stepColumnCount = Math.max(lastColumn - LABEL_COLUMN, 1)

  const stepColumnTracks = Array.from({ length: stepColumnCount }, (_, i) =>
    collapsedColumns.has(FIRST_STEP_COLUMN + i)
      ? 'var(--sm-collapsed-col-width)'
      : 'minmax(var(--sm-col-min), 1fr)',
  )

  return {
    goals,
    steps,
    swimLanes,
    lastColumn,
    stepColumnTracks,
    collapsedColumnCount: collapsedColumns.size,
    flexibleColumnCount: stepColumnCount - collapsedColumns.size,
    tasksByCell,
  }
}
