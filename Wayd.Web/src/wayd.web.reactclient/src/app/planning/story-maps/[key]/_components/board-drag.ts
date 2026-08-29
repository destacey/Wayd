import { BoardLayout } from './board-layout'

/**
 * Resolving a drop on the board.
 *
 * Goals, steps, tasks, and swim lanes each drag along their own axis, so every drop starts by
 * working out which kind moved — ids are opaque GUIDs, so the layout is indexed once per drag.
 *
 * A target is either a sibling of the same kind or, for tasks and step-less goals, an empty slot.
 * Dropping on a sibling takes its place; dropping on a slot appends to it.
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
 * Which side of the hovered node a drop lands on, decided by which half of it the pointer is in.
 *
 * Using the pointer rather than list indices keeps the insertion point inside a node's own box, so
 * the seam between adjacent goals stays unambiguous: the right half of goal 1's last step means
 * "last in goal 1" and the left half of goal 2's first step means "first in goal 2", even though
 * both are the same pixel column.
 */
export type DropSide = 'before' | 'after'

/**
 * Prefix marking the steps-row slot of a goal that has no steps — the drop target for joining that
 * goal, since it has no step to aim at. Goal headers are never targets.
 */
export const EMPTY_STEP_SLOT_PREFIX = 'step-slot:'

export const emptyStepSlotId = (goalId: string) =>
  `${EMPTY_STEP_SLOT_PREFIX}${goalId}`

/** The goal id behind an empty-slot target, or null when the id is not one. */
export const parseEmptyStepSlotId = (id: string) =>
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
  /**
   * Every task-cell id the board renders — one per rendered step × expanded lane. Cell ids are
   * structured strings, so a well-formed one can still name a cell a collapse has hidden; targets
   * are checked against this set rather than by parsing.
   */
  renderedCellIds: Set<string>
}

/**
 * Index every draggable id once per drag, so drop resolution is plain lookups.
 *
 * Anything a collapse hides is left out — indexing it would let `resolveDrop` compute a landing
 * position among siblings the user cannot see. A collapsed node's own header stays draggable, so
 * goals and lanes can still be reordered while collapsed.
 */
export const buildDragIndex = (layout: BoardLayout): BoardDragIndex => {
  const kindById = new Map<string, DragKind>()
  const goalIdByStepId = new Map<string, string>()
  const cellByTaskId = new Map<string, { stepId: string; swimLaneId: string }>()

  const collapsedLaneIds = new Set(
    layout.swimLanes
      .filter(({ isCollapsed }) => isCollapsed)
      .map(({ swimLane }) => swimLane.id),
  )

  const expandedLaneIds = layout.swimLanes
    .filter(({ isCollapsed }) => !isCollapsed)
    .map(({ swimLane }) => swimLane.id)

  const renderedCellIds = new Set<string>()

  for (const { goal } of layout.goals) kindById.set(goal.id, 'goal')
  for (const { swimLane } of layout.swimLanes) {
    kindById.set(swimLane.id, 'swimLane')
  }
  for (const { step, goalId } of layout.steps) {
    kindById.set(step.id, 'step')
    goalIdByStepId.set(step.id, goalId)

    for (const swimLaneId of expandedLaneIds) {
      renderedCellIds.add(taskCellId(step.id, swimLaneId))
    }

    for (const task of step.tasks) {
      if (collapsedLaneIds.has(task.swimLaneId)) continue

      kindById.set(task.id, 'task')
      cellByTaskId.set(task.id, {
        stepId: step.id,
        swimLaneId: task.swimLaneId,
      })
    }
  }

  return { kindById, goalIdByStepId, cellByTaskId, renderedCellIds }
}

/**
 * The index to send as `newOrder` when dropping `activeId` on the given `side` of `overId`.
 *
 * The server and the optimistic patches both remove-then-insert, so this is computed against the
 * list with the dragged item already taken out: "before" the target is its index, "after" is one
 * past — then one less again if the item currently sits earlier in the same list, because removing
 * it pulls the target back a place. An item arriving from another parent shifts nothing.
 *
 * Returns -1 when the target is not a sibling, null when the move is a no-op.
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
 * Whether `overId` is somewhere `activeId` may legally land. Filters collision detection during the
 * drag so an illegal target never highlights or draws an insertion line.
 *
 * Deliberately coarser than `resolveDrop`: it answers "could this ever be a target", ignoring
 * no-ops like dropping onto yourself, so a node stays responsive under its own cursor.
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
    // A task lands on another task, or on a rendered (step × lane) cell — including an empty one.
    case 'task':
      return overKind === 'task' || index.renderedCellIds.has(overId)
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

  // Dropped on a cell rather than a card — the empty space below the last one, or an empty cell.
  // Either way the task appends to the end. Parseable is not enough: a hidden cell parses fine.
  const cell = index.renderedCellIds.has(overId)
    ? parseTaskCellId(overId)
    : null
  if (cell) {
    const existing =
      layout.tasksByCell.get(`${cell.stepId}:${cell.swimLaneId}`) ?? []
    const changedCell =
      cell.stepId !== from.stepId || cell.swimLaneId !== from.swimLaneId

    // Appending within its own cell means the task is removed first, so the end is one index lower.
    const newOrder = changedCell ? existing.length : existing.length - 1

    // Already the last task here, so appending changes nothing.
    if (!changedCell && existing[existing.length - 1]?.id === activeId) {
      return null
    }

    return {
      kind: 'task',
      taskId: activeId,
      targetStepId: cell.stepId,
      targetSwimLaneId: cell.swimLaneId,
      newOrder,
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
    changedCell: to.stepId !== from.stepId || to.swimLaneId !== from.swimLaneId,
  }
}

/**
 * What a drag should show while it is in flight — the counterpart to {@link resolveDrop}, which says
 * what the drop will do. Both read the same `(activeId, overId, side)`, so the preview and the
 * outcome cannot disagree.
 */

/** The one cell drawing the insertion line for the current seam, and which of its edges. */
export interface StepSeam {
  stepId: string
  edge: DropSide
}

/**
 * Which step cell draws the insertion line.
 *
 * A seam has two names — "after step A" and "before step B" — that resolve to the same index, so
 * letting each cell light its own hovered edge gives one landing position two appearances that swap
 * as the pointer crosses a midpoint. This picks a single cell per seam: normally the step that
 * follows it, drawing a leading edge. A goal's last step has no follower, so that seam alone keeps a
 * trailing edge.
 */
export const resolveStepSeam = (
  layout: BoardLayout,
  index: BoardDragIndex,
  activeId: string | null,
  overId: string | null,
  side: DropSide,
): StepSeam | null => {
  if (!activeId || index.kindById.get(activeId) !== 'step') return null
  if (!overId || index.kindById.get(overId) !== 'step') return null

  const hovered = layout.steps.find((s) => s.step.id === overId)
  if (!hovered) return null

  if (side === 'before') return { stepId: hovered.step.id, edge: 'before' }

  const next = layout.steps.find(
    (s) =>
      s.goalId === hovered.goalId && s.indexInGoal === hovered.indexInGoal + 1,
  )

  return next
    ? { stepId: next.step.id, edge: 'before' }
    : { stepId: hovered.step.id, edge: 'after' }
}

/**
 * The goal a dragged step would join, whose run of step columns is outlined as the destination.
 * Null when the drag stays inside its current goal — a reorder needs no destination marker — and for
 * a step-less goal, whose slot already marks itself as the target.
 */
export const resolveReceivingGoalId = (
  index: BoardDragIndex,
  activeId: string | null,
  overId: string | null,
): string | null => {
  if (!activeId || !overId) return null
  if (index.kindById.get(activeId) !== 'step') return null

  const fromGoalId = index.goalIdByStepId.get(activeId)

  // Either hovering a sibling step, or the empty slot of a goal with no steps of its own.
  const toGoalId =
    index.goalIdByStepId.get(overId) ?? parseEmptyStepSlotId(overId)

  return toGoalId && toGoalId !== fromGoalId ? toGoalId : null
}

/**
 * The cell a dragged task would land in, or null when that is the cell it already sits in. A task's
 * parent is its step crossed with its swim lane, so either axis changing is a reparent.
 */
export const resolveReceivingCellId = (
  index: BoardDragIndex,
  activeId: string | null,
  overId: string | null,
): string | null => {
  if (!activeId || !overId) return null
  if (index.kindById.get(activeId) !== 'task') return null

  const from = index.cellByTaskId.get(activeId)
  if (!from) return null

  // The target is either an empty cell, or a card — in which case the cell is the one it sits in.
  const overCell = index.cellByTaskId.get(overId)
  const to = overCell
    ? taskCellId(overCell.stepId, overCell.swimLaneId)
    : index.renderedCellIds.has(overId)
      ? overId
      : null

  return to && to !== taskCellId(from.stepId, from.swimLaneId) ? to : null
}
