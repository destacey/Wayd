import { SprintScopeDto, SprintScopeMeasureDto } from '@/src/services/wayd-api'
import { SprintMetricFigures, sprintMetricValues } from './sprint-metric-values'

/** What became of the sprint's committed and added work, from its scope. */
export interface SprintScopeFigures {
  committed: number
  added: number
  removed: number
  carriedOver: number
  descoped: number
  /** Completed of committed ÷ committed; null when nothing was committed. */
  sayDo: number | null
  /**
   * Velocity ÷ committed, capped at 1: how much of what the team committed to
   * it delivered, letting finished added work stand in for committed work.
   * Null when nothing was committed.
   */
  predictability: number | null
}

/**
 * The figures a sprint's overview shows, in its own estimate or by item count.
 *
 * Completed work and the completion rate come from the sprint's scope once it
 * has one — history read in full, and the commitment point passed — since scope
 * keeps work carried over or finished and moved out, which the sprint's current
 * items have lost. Until then they come from the current items, and `scope` is
 * null. Work in progress and not started is always what is in the sprint now.
 */
export interface SprintOverviewFigures {
  completed: number
  /** What completion is measured against: everything in scope but the descoped work. */
  completionBase: number
  inProgress: number
  notStarted: number
  /** Everything in the sprint now, which in progress and not started are shares of. */
  currentTotal: number
  scope: SprintScopeFigures | null
}

/** Whether a scope report has figures to show at `now`. */
export const scopeIsReadable = (
  scope: SprintScopeDto | undefined,
  now: Date = new Date(),
): scope is SprintScopeDto =>
  !!scope &&
  !scope.historyIncomplete &&
  now.getTime() >= new Date(scope.effectiveStart).getTime()

export const sprintOverviewFigures = (
  metrics: SprintMetricFigures | undefined,
  scope: SprintScopeDto | undefined,
  byCount: boolean,
  now: Date = new Date(),
): SprintOverviewFigures => {
  const current = sprintMetricValues(metrics, byCount)

  if (!scopeIsReadable(scope, now)) {
    return {
      completed: current.completed,
      completionBase: current.total,
      inProgress: current.inProgress,
      notStarted: current.notStarted,
      currentTotal: current.total,
      scope: null,
    }
  }

  const value = (m: SprintScopeMeasureDto) => (byCount ? m.count : m.estimate)
  const { totals } = scope

  return {
    completed: value(totals.completed),
    completionBase: value(totals.total) - value(totals.descoped),
    inProgress: current.inProgress,
    notStarted: current.notStarted,
    currentTotal: current.total,
    scope: {
      committed: value(totals.committed),
      added: value(totals.added),
      removed: value(totals.removed),
      carriedOver: value(totals.carriedOver),
      descoped: value(totals.descoped),
      sayDo: (byCount ? totals.sayDoCount : totals.sayDoEstimate) ?? null,
      predictability:
        value(totals.committed) > 0
          ? Math.min(value(totals.completed) / value(totals.committed), 1)
          : null,
    },
  }
}
