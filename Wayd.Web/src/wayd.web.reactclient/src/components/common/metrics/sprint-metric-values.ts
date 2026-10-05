/** A sprint's work, as item counts and as estimates in the sprint's sizing method. */
export interface SprintMetricFigures {
  totalWorkItems: number
  totalEstimate: number
  completedWorkItems: number
  completedEstimate: number
  inProgressWorkItems: number
  inProgressEstimate: number
  notStartedWorkItems: number
  notStartedEstimate: number
}

export interface SprintMetricValues {
  total: number
  completed: number
  inProgress: number
  notStarted: number
}

/**
 * The figures a sprint's metric cards show: its estimates in its own sizing method, or its item counts when
 * `byCount` is set. Zero throughout while the metrics are loading.
 */
export const sprintMetricValues = (
  metrics: SprintMetricFigures | undefined,
  byCount: boolean,
): SprintMetricValues => {
  if (!metrics) return { total: 0, completed: 0, inProgress: 0, notStarted: 0 }
  return byCount
    ? {
        total: metrics.totalWorkItems,
        completed: metrics.completedWorkItems,
        inProgress: metrics.inProgressWorkItems,
        notStarted: metrics.notStartedWorkItems,
      }
    : {
        total: metrics.totalEstimate,
        completed: metrics.completedEstimate,
        inProgress: metrics.inProgressEstimate,
        notStarted: metrics.notStartedEstimate,
      }
}
