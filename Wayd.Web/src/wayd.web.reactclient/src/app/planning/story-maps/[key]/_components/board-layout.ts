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
}

export interface SwimLanePlacement {
  swimLane: StoryMapSwimLaneDto
  /** 1-based grid row line of the lane's full-width header banner. */
  headerRow: number
  /**
   * 1-based grid row line this lane's task cells occupy, directly under the header — or null when
   * the lane is collapsed, in which case it claims no task row at all.
   */
  row: number | null
  index: number
  /** Collapsed lanes render as the banner alone; their tasks are hidden, not filtered. */
  isCollapsed: boolean
}

export interface BoardLayout {
  goals: GoalPlacement[]
  steps: StepPlacement[]
  swimLanes: SwimLanePlacement[]
  /** 1-based line number of the right-most column, used to spot outer-edge cells. */
  lastColumn: number
  /**
   * Number of step tracks the grid template must declare. NOT `steps.length` — a step-less goal
   * still claims a placeholder track, and declaring too few makes the browser size the overflow
   * columns by content instead of `1fr`, so goals stop sharing width equally.
   */
  stepColumnCount: number
  /** Tasks keyed by `${stepId}:${swimLaneId}`, each list sorted by order. */
  tasksByCell: Map<string, StoryMapTaskDto[]>
}

/** Key for a task cell — the intersection of a step column and a swim-lane row. */
export const cellKey = (stepId: string, swimLaneId: string) =>
  `${stepId}:${swimLaneId}`

const byOrder = <T extends { order: number }>(items: T[]): T[] =>
  [...items].sort((a, b) => a.order - b.order)

/**
 * Walk the map in display order, assigning each step the next column and each goal a header span
 * covering its steps. A step-less goal still claims one column so its header stays reachable.
 *
 * `collapsedSwimLaneIds` is transient view state, so it changes geometry rather than data: a
 * collapsed lane claims its banner row and nothing else, and the lanes below shuffle up. Row numbers
 * are therefore accumulated, not derived from the lane's index.
 */
export const buildBoardLayout = (
  map: StoryMapDetailsDto,
  collapsedSwimLaneIds: ReadonlySet<string> = new Set(),
): BoardLayout => {
  const goals: GoalPlacement[] = []
  const steps: StepPlacement[] = []

  let column = FIRST_STEP_COLUMN

  byOrder(map.goals).forEach((goal, index) => {
    const goalSteps = byOrder(goal.steps)
    const columnStart = column

    goalSteps.forEach((step, indexInGoal) => {
      steps.push({ step, goalId: goal.id, column, indexInGoal })
      column += 1
    })

    // An empty goal still occupies a single placeholder column.
    const isPlaceholderColumn = goalSteps.length === 0
    const columnSpan = Math.max(goalSteps.length, 1)
    if (isPlaceholderColumn) column += 1

    goals.push({ goal, columnStart, columnSpan, index, isPlaceholderColumn })
  })

  // An expanded lane occupies two rows — a full-width header banner, then the row of task cells
  // under it — and a collapsed one just the banner, so rows are handed out as the walk proceeds.
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
      // A collapsed lane renders no cells, so its tasks must not appear here either — this map is
      // what drop resolution counts siblings in, and a hidden cell is not a legal landing place.
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

  return {
    goals,
    steps,
    swimLanes,
    lastColumn,
    // Floored at 1 so a map with no goals still has a track for the grid template to declare.
    stepColumnCount: Math.max(lastColumn - LABEL_COLUMN, 1),
    tasksByCell,
  }
}
