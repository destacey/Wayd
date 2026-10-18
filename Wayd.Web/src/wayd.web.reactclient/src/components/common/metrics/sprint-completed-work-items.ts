import { PlanningIntervalIterationMetricsResponse } from '@/src/services/wayd-api'

/**
 * The work items completed in a PI iteration's sprints alone. The iteration's
 * `completedWorkItems` also holds what its Kanban teams completed, which has no
 * scope behind it, so a completion rate or health against `totalWorkItems`
 * reads this instead.
 */
export const sprintCompletedWorkItems = (
  metrics: Pick<PlanningIntervalIterationMetricsResponse, 'sprintMetrics'>,
): number =>
  metrics.sprintMetrics.reduce(
    (completed, sprint) => completed + sprint.completedWorkItems,
    0,
  )
