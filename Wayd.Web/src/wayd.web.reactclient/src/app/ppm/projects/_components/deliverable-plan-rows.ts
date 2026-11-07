import {
  ProjectDeliverableDto,
  ProjectPlanNodeDto,
} from '@/src/services/wayd-api'

/** The heading the plan work no deliverable is served by sits under. */
export const NO_DELIVERABLE = 'No deliverable'

/** One plan task under one deliverable heading of the plan grouped by deliverable. */
export interface DeliverablePlanRow {
  /** Unique per heading, since a task serving two deliverables appears under both. */
  rowId: string
  /** The deliverable's name, or {@link NO_DELIVERABLE}. */
  deliverableName: string
  /** The deliverable, or null under {@link NO_DELIVERABLE}. */
  deliverable: ProjectDeliverableDto | null
  task: ProjectPlanNodeDto
}

const isTask = (node: ProjectPlanNodeDto) => node.nodeType === 'Task'

/** Whether the task or any task beneath it serves a deliverable. */
const servesAny = (task: ProjectPlanNodeDto): boolean =>
  (task.deliverableIds?.length ?? 0) > 0 ||
  (task.children ?? []).filter(isTask).some(servesAny)

/**
 * The plan regrouped by deliverable, in the deliverables' order with
 * {@link NO_DELIVERABLE} last.
 *
 * Each deliverable lists the tasks linked to it — a linked summary task
 * stands for its subtasks, so a subtask is listed under a deliverable only
 * when no task above it already is. A task serving two deliverables appears
 * under both. Under {@link NO_DELIVERABLE} is the topmost work nothing serves
 * a deliverable through: a task with no link, beneath no linked task, whose
 * subtasks have no link either.
 */
export const buildDeliverablePlanRows = (
  plan: ProjectPlanNodeDto[],
  deliverables: ProjectDeliverableDto[],
): DeliverablePlanRow[] => {
  const byId = new Map(deliverables.map((d) => [d.id, d]))
  const linked = new Map<string, DeliverablePlanRow[]>()
  const unlinked: DeliverablePlanRow[] = []

  /** @param covered The deliverables a task above this one already serves. */
  const visit = (task: ProjectPlanNodeDto, covered: ReadonlySet<string>) => {
    if (!servesAny(task)) {
      if (covered.size === 0) {
        unlinked.push({
          rowId: `none:${task.id}`,
          deliverableName: NO_DELIVERABLE,
          deliverable: null,
          task,
        })
      }
      return
    }

    const below = new Set(covered)
    for (const id of task.deliverableIds ?? []) {
      below.add(id)
      const deliverable = byId.get(id)
      if (!deliverable || covered.has(id)) continue
      const rows = linked.get(id) ?? []
      rows.push({
        rowId: `${id}:${task.id}`,
        deliverableName: deliverable.name,
        deliverable,
        task,
      })
      linked.set(id, rows)
    }

    ;(task.children ?? [])
      .filter(isTask)
      .forEach((child) => visit(child, below))
  }

  for (const stage of plan) {
    ;(stage.children ?? [])
      .filter(isTask)
      .forEach((task) => visit(task, new Set()))
  }

  return [...deliverables.flatMap((d) => linked.get(d.id) ?? []), ...unlinked]
}
