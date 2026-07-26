import { BoardLayout } from './board-layout'

/**
 * Resolving a drop on the board.
 *
 * Four things drag — goals, steps, tasks, and swim lanes — and each is constrained to its own axis,
 * so the first job on every drop is working out which kind moved. Ids alone cannot say (they are all
 * opaque GUIDs), so the layout is indexed once per drag and the active id looked up.
 *
 * A drop target is either a sibling (drop onto another node of the same kind) or, for tasks only, an
 * empty cell. Dropping a task on a card means "take that card's place"; dropping it on a cell means
 * "append to that cell". Both resolve to a (step, swim lane, order) triple.
 */

export type DragKind = 'goal' | 'step' | 'task' | 'swimLane'

/** Prefix marking a droppable task cell, so a cell id is distinguishable from a node id. */
export const TASK_CELL_PREFIX = 'cell:'

export const taskCellId = (stepId: string, swimLaneId: string) =>
  `${TASK_CELL_PREFIX}${stepId}:${swimLaneId}`

const parseTaskCellId = (id: string) => {
  if (!id.startsWith(TASK_CELL_PREFIX)) return null
  const [stepId, swimLaneId] = id.slice(TASK_CELL_PREFIX.length).split(':')
  return stepId && swimLaneId ? { stepId, swimLaneId } : null
}

/**
 * Which side of the hovered node a drop lands on.
 *
 * Decided by which half of the target the pointer is in, NOT by comparing list indices. That keeps
 * the insertion point inside a node's own box instead of on the border between two, so the seam
 * between adjacent goals is never ambiguous: the right half of goal 1's last step means "last in
 * goal 1", the left half of goal 2's first step means "first in goal 2", even though the two are the
 * same pixel column.
 */
export type DropSide = 'before' | 'after'

/**
 * Prefix marking the steps-row slot of a goal that has no steps.
 *
 * A step-less goal has no step to aim at, so its placeholder column is the target for joining that
 * goal — the goal header above it is never a target, which keeps the goals row inert during a step
 * drag. Populated goals need nothing equivalent: their ends are reachable via the outer half of
 * their first and last steps.
 */
export const EMPTY_STEP_SLOT_PREFIX = 'step-slot:'

export const emptyStepSlotId = (goalId: string) =>
  `${EMPTY_STEP_SLOT_PREFIX}${goalId}`

const parseEmptyStepSlotId = (id: string) =>
  id.startsWith(EMPTY_STEP_SLOT_PREFIX)
    ? id.slice(EMPTY_STEP_SLOT_PREFIX.length)
    : null

export interface GoalReorder {
  kind: 'goal'
  goalId: string
  newOrder: number
}

export interface StepMove {
  kind: 'step'
  stepId: string
  /** Set when the step changed goal; undefined for a reorder inside its current goal. */
  targetGoalId?: string
  newOrder: number
}

export interface TaskMove {
  kind: 'task'
  taskId: string
  targetStepId: string
  targetSwimLaneId: string
  newOrder: number
  /** False when the task stayed in its cell and only changed position. */
  changedCell: boolean
}

export interface SwimLaneReorder {
  kind: 'swimLane'
  swimLaneId: string
  newOrder: number
}

export type DropResult = GoalReorder | StepMove | TaskMove | SwimLaneReorder

export interface BoardDragIndex {
  kindById: Map<string, DragKind>
  goalIdByStepId: Map<string, string>
  cellByTaskId: Map<string, { stepId: string; swimLaneId: string }>
}

/** Index every draggable id once per drag, so drop resolution is plain lookups. */
export const buildDragIndex = (layout: BoardLayout): BoardDragIndex => {
  const kindById = new Map<string, DragKind>()
  const goalIdByStepId = new Map<string, string>()
  const cellByTaskId = new Map<string, { stepId: string; swimLaneId: string }>()

  for (const { goal } of layout.goals) kindById.set(goal.id, 'goal')
  for (const { swimLane } of layout.swimLanes) {
    kindById.set(swimLane.id, 'swimLane')
  }
  for (const { step, goalId } of layout.steps) {
    kindById.set(step.id, 'step')
    goalIdByStepId.set(step.id, goalId)
    for (const task of step.tasks) {
      kindById.set(task.id, 'task')
      cellByTaskId.set(task.id, {
        stepId: step.id,
        swimLaneId: task.swimLaneId,
      })
    }
  }

  return { kindById, goalIdByStepId, cellByTaskId }
}

/**
 * The index to send as `newOrder` when dropping `activeId` on the given `side` of `overId`.
 *
 * Both the server and the optimistic patches use remove-then-insert: the item is pulled out of the
 * list first, then spliced back in at `newOrder`. So the position is computed against the list *with
 * the dragged item already removed* — which is what makes the arithmetic here asymmetric.
 *
 * Dropping "before" the target means taking the target's index. Dropping "after" means one past it.
 * Both then shift down by one if the dragged item currently sits earlier in this same list, since
 * removing it pulls the target back a place. An item arriving from a different parent is not in this
 * list at all, so nothing shifts.
 *
 * Returns -1 when the target is not a sibling, and null when the move is a no-op.
 */
const landingIndex = (
  siblings: string[],
  activeId: string,
  overId: string,
  side: DropSide,
): number | null => {
  const overIndex = siblings.indexOf(overId)
  if (overIndex === -1) return -1

  const activeIndex = siblings.indexOf(activeId)
  const base = side === 'after' ? overIndex + 1 : overIndex
  const newOrder = activeIndex !== -1 && activeIndex < base ? base - 1 : base

  // Landing back where it started changes nothing.
  return activeIndex !== -1 && newOrder === activeIndex ? null : newOrder
}

/**
 * Whether `overId` is somewhere `activeId` may legally land.
 *
 * Used to filter collision detection during the drag, so an illegal target never highlights, never
 * opens a gap, and never draws an insertion line. Without it a goal dragged over a task looks
 * perfectly droppable and then silently does nothing on release, which reads as a broken drag rather
 * than a forbidden one.
 *
 * This is deliberately coarser than `resolveDrop`: it answers "could this ever be a target",
 * ignoring no-ops like dropping onto yourself, so the item under the cursor still responds while a
 * drag hovers its own position.
 */
export const isValidDropTarget = (
  index: BoardDragIndex,
  activeId: string,
  overId: string,
): boolean => {
  const kind = index.kindById.get(activeId)
  if (!kind) return false

  const overKind = index.kindById.get(overId)

  switch (kind) {
    // Goals and swim lanes only ever reorder among their own kind.
    case 'goal':
      return overKind === 'goal'
    case 'swimLane':
      return overKind === 'swimLane'
    // A step reorders among steps, or lands in the empty steps slot under a step-less goal. The
    // goals row itself is never a target.
    case 'step':
      return overKind === 'step' || parseEmptyStepSlotId(overId) !== null
    // A task lands on another task, or on a (step × lane) cell — including an empty one.
    case 'task':
      return overKind === 'task' || parseTaskCellId(overId) !== null
  }
}

/**
 * Work out what a completed drag means. Returns null when the drop is a no-op or lands somewhere
 * that makes no sense for the thing being dragged (a task onto a goal, say).
 */
export const resolveDrop = (
  layout: BoardLayout,
  index: BoardDragIndex,
  activeId: string,
  overId: string,
  side: DropSide = 'before',
): DropResult | null => {
  const kind = index.kindById.get(activeId)
  if (!kind) return null

  if (kind === 'goal') {
    // Goals only reorder among goals.
    if (index.kindById.get(overId) !== 'goal') return null
    if (activeId === overId) return null

    const goalIds = layout.goals.map((g) => g.goal.id)
    const newOrder = landingIndex(goalIds, activeId, overId, side)
    return newOrder === null || newOrder === -1
      ? null
      : { kind: 'goal', goalId: activeId, newOrder }
  }

  if (kind === 'swimLane') {
    if (index.kindById.get(overId) !== 'swimLane') return null
    if (activeId === overId) return null

    const laneIds = layout.swimLanes.map((l) => l.swimLane.id)
    const newOrder = landingIndex(laneIds, activeId, overId, side)
    if (newOrder === null || newOrder === -1) return null

    // The default lane is pinned at position 0 and nothing may displace it.
    if (newOrder === 0 && layout.swimLanes[0]?.swimLane.isDefault) return null

    return { kind: 'swimLane', swimLaneId: activeId, newOrder }
  }

  if (kind === 'step') {
    // The placeholder column of a step-less goal: the step becomes that goal's only step.
    const slotGoalId = parseEmptyStepSlotId(overId)
    if (slotGoalId) {
      const currentGoalId = index.goalIdByStepId.get(activeId)
      if (currentGoalId === slotGoalId) return null

      return {
        kind: 'step',
        stepId: activeId,
        targetGoalId: slotGoalId,
        newOrder: 0,
      }
    }

    if (index.kindById.get(overId) !== 'step' || activeId === overId) {
      return null
    }

    const fromGoalId = index.goalIdByStepId.get(activeId)
    const toGoalId = index.goalIdByStepId.get(overId)
    if (!fromGoalId || !toGoalId) return null

    const targetSiblings = layout.steps
      .filter((s) => s.goalId === toGoalId)
      .map((s) => s.step.id)
    const newOrder = landingIndex(targetSiblings, activeId, overId, side)
    if (newOrder === null || newOrder === -1) return null

    return fromGoalId === toGoalId
      ? { kind: 'step', stepId: activeId, newOrder }
      : { kind: 'step', stepId: activeId, targetGoalId: toGoalId, newOrder }
  }

  // ── Tasks ──
  const from = index.cellByTaskId.get(activeId)
  if (!from) return null

  // Dropped on a cell (including an empty one): append to it.
  const cell = parseTaskCellId(overId)
  if (cell) {
    const existing = layout.tasksByCell.get(`${cell.stepId}:${cell.swimLaneId}`)
    const changedCell =
      cell.stepId !== from.stepId || cell.swimLaneId !== from.swimLaneId
    if (!changedCell) return null

    return {
      kind: 'task',
      taskId: activeId,
      targetStepId: cell.stepId,
      targetSwimLaneId: cell.swimLaneId,
      newOrder: existing?.length ?? 0,
      changedCell,
    }
  }

  // Dropped on another task: take its place in whatever cell it occupies.
  if (index.kindById.get(overId) !== 'task' || activeId === overId) return null

  const to = index.cellByTaskId.get(overId)
  if (!to) return null

  const siblings =
    layout.tasksByCell.get(`${to.stepId}:${to.swimLaneId}`)?.map((t) => t.id) ??
    []
  const newOrder = landingIndex(siblings, activeId, overId, side)
  if (newOrder === null || newOrder === -1) return null

  return {
    kind: 'task',
    taskId: activeId,
    targetStepId: to.stepId,
    targetSwimLaneId: to.swimLaneId,
    newOrder,
    changedCell:
      to.stepId !== from.stepId || to.swimLaneId !== from.swimLaneId,
  }
}
