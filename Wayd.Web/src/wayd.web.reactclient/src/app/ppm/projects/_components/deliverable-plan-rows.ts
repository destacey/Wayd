import {
  ProjectDeliverableDto,
  ProjectPlanNodeDto,
} from '@/src/services/wayd-api'

/** The heading the plan work no deliverable is served by sits under. */
export const NO_DELIVERABLE = 'No deliverable'

/**
 * The group key of {@link NO_DELIVERABLE}. Rows group by id rather than name,
 * so a deliverable that happens to be named "No deliverable" stays apart.
 */
export const NO_DELIVERABLE_KEY = ''

/** One plan task under one deliverable heading of the plan grouped by deliverable. */
export interface DeliverablePlanRow {
  /** Unique per heading, since a task serving two deliverables appears under both. */
  rowId: string
  /** What the rows group on: the deliverable's id, or {@link NO_DELIVERABLE_KEY}. */
  groupKey: string
  /** The deliverable's name, or {@link NO_DELIVERABLE}. */
  deliverableName: string
  /** The deliverable, or null under {@link NO_DELIVERABLE}. */
  deliverable: ProjectDeliverableDto | null
  task: ProjectPlanNodeDto
}

const isTask = (node: ProjectPlanNodeDto) => node.nodeType === 'Task'

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

  // Only deliverables that are known count: a link to one deleted since the
  // plan was read would otherwise drop its task from every heading.
  const knownLinks = (task: ProjectPlanNodeDto) =>
    (task.deliverableIds ?? []).filter((id) => byId.has(id))

  /** Whether the task or any task beneath it serves a known deliverable. */
  const servesAny = (task: ProjectPlanNodeDto): boolean =>
    knownLinks(task).length > 0 ||
    (task.children ?? []).filter(isTask).some(servesAny)
  const linked = new Map<string, DeliverablePlanRow[]>()
  const unlinked: DeliverablePlanRow[] = []

  /** @param covered The deliverables a task above this one already serves. */
  const visit = (task: ProjectPlanNodeDto, covered: ReadonlySet<string>) => {
    if (!servesAny(task)) {
      if (covered.size === 0) {
        unlinked.push({
          rowId: `none:${task.id}`,
          groupKey: NO_DELIVERABLE_KEY,
          deliverableName: NO_DELIVERABLE,
          deliverable: null,
          task,
        })
      }
      return
    }

    const below = new Set(covered)
    for (const id of knownLinks(task)) {
      below.add(id)
      if (covered.has(id)) continue
      const deliverable = byId.get(id)!
      const rows = linked.get(id) ?? []
      rows.push({
        rowId: `${id}:${task.id}`,
        groupKey: id,
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
