import {
  MoveStepRequest,
  MoveTaskRequest,
  StoryMapDetailsDto,
} from '@/src/services/wayd-api'

/**
 * Optimistic cache edits for the story map board, applied to an RTK Query draft before the server
 * responds and undone if it fails.
 *
 * They mutate in place (Immer drafts) and must reproduce what the domain does server-side, or the
 * board visibly jumps when the refetch reconciles. `order` is contiguous and scoped to a parent — a
 * goal for steps, a (step × swim lane) cell for tasks — so a move renumbers both the list the node
 * left and the one it joined.
 */

/** Move an ordered sibling to a new position and renumber the list contiguously. */
export const reorderInPlace = <T extends { id: string; order: number }>(
  items: T[],
  id: string,
  newOrder: number,
) => {
  const ordered = [...items].sort((a, b) => a.order - b.order)
  const from = ordered.findIndex((x) => x.id === id)
  if (from === -1) return

  const [moved] = ordered.splice(from, 1)
  const to = Math.max(0, Math.min(newOrder, ordered.length))
  ordered.splice(to, 0, moved)
  ordered.forEach((item, index) => {
    const target = items.find((x) => x.id === item.id)
    if (target) target.order = index
  })
}

/** Re-parent a step to another goal, renumbering both goals. */
export const applyMoveStep = (
  draft: StoryMapDetailsDto,
  stepId: string,
  request: MoveStepRequest,
) => {
  const fromGoal = draft.goals.find((g) => g.steps.some((s) => s.id === stepId))
  const toGoal = draft.goals.find((g) => g.id === request.targetGoalId)
  if (!fromGoal || !toGoal) return

  const step = fromGoal.steps.find((s) => s.id === stepId)
  if (!step) return

  // Same-goal drops are sent to the reorder endpoint instead, but handle it rather than
  // double-inserting the step if that ever changes.
  if (fromGoal.id === toGoal.id) {
    reorderInPlace(fromGoal.steps, stepId, request.newOrder)
    return
  }

  fromGoal.steps = fromGoal.steps.filter((s) => s.id !== stepId)
  fromGoal.steps
    .sort((a, b) => a.order - b.order)
    .forEach((s, i) => {
      s.order = i
    })

  step.goalId = toGoal.id
  const destination = [...toGoal.steps].sort((a, b) => a.order - b.order)
  const at = Math.max(0, Math.min(request.newOrder, destination.length))
  destination.splice(at, 0, step)
  destination.forEach((s, i) => {
    s.order = i
  })
  toGoal.steps = destination
}

/**
 * Move a task to a (step × swim lane) cell. Covers a same-cell reorder too — there is no separate
 * reorder endpoint for tasks.
 */
export const applyMoveTask = (
  draft: StoryMapDetailsDto,
  taskId: string,
  request: MoveTaskRequest,
) => {
  const steps = draft.goals.flatMap((g) => g.steps)
  const fromStep = steps.find((s) => s.tasks.some((t) => t.id === taskId))
  const toStep = steps.find((s) => s.id === request.targetStepId)
  if (!fromStep || !toStep) return

  const task = fromStep.tasks.find((t) => t.id === taskId)
  if (!task) return

  const fromLaneId = task.swimLaneId
  fromStep.tasks = fromStep.tasks.filter((t) => t.id !== taskId)

  task.stepId = toStep.id
  task.swimLaneId = request.targetSwimLaneId

  const destination = toStep.tasks
    .filter((t) => t.swimLaneId === request.targetSwimLaneId)
    .sort((a, b) => a.order - b.order)
  const at = Math.max(0, Math.min(request.newOrder, destination.length))
  destination.splice(at, 0, task)
  destination.forEach((t, i) => {
    t.order = i
  })

  toStep.tasks = [
    ...toStep.tasks.filter((t) => t.swimLaneId !== request.targetSwimLaneId),
    ...destination,
  ]

  // Close the gap left behind when the task changed cell.
  if (fromStep.id !== toStep.id || fromLaneId !== task.swimLaneId) {
    fromStep.tasks
      .filter((t) => t.swimLaneId === fromLaneId)
      .sort((a, b) => a.order - b.order)
      .forEach((t, i) => {
        t.order = i
      })
  }
}

/**
 * Remove a swim lane, reassigning its tasks to the default lane rather than deleting them — what
 * `RemoveSwimLaneCommand` does server-side. The default lane cannot be removed.
 */
export const applyRemoveSwimLane = (
  draft: StoryMapDetailsDto,
  swimLaneId: string,
) => {
  const defaultLane = draft.swimLanes.find((l) => l.isDefault)
  if (!defaultLane || defaultLane.id === swimLaneId) return

  // Append after whatever the default lane already holds in that step, keeping relative order.
  // Rewriting the lane id alone leaves two sets sharing one order sequence, so the cell interleaves
  // and then jumps when the refetch corrects it.
  for (const goal of draft.goals) {
    for (const step of goal.steps) {
      const moving = step.tasks
        .filter((t) => t.swimLaneId === swimLaneId)
        .sort((a, b) => a.order - b.order)
      if (moving.length === 0) continue

      const existing = step.tasks
        .filter((t) => t.swimLaneId === defaultLane.id)
        .sort((a, b) => a.order - b.order)

      existing.forEach((task, index) => {
        task.order = index
      })
      moving.forEach((task, index) => {
        task.swimLaneId = defaultLane.id
        task.order = existing.length + index
      })
    }
  }

  draft.swimLanes = draft.swimLanes
    .filter((l) => l.id !== swimLaneId)
    .sort((a, b) => a.order - b.order)
    .map((lane, index) => ({ ...lane, order: index }))
}

/** Link or unlink one persona, returning the full list the API expects. */
export const togglePersonaId = (personaIds: string[], personaId: string) =>
  personaIds.includes(personaId)
    ? personaIds.filter((id) => id !== personaId)
    : [...personaIds, personaId]
