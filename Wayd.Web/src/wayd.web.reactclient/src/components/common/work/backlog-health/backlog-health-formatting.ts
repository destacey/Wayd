import {
  BacklogHealthCheckDto,
  TeamBacklogHealthDto,
} from '@/src/services/wayd-api'

/** Mirrors the API's BacklogHealthCheck enum, which the DTOs carry as ids. */
export enum BacklogHealthCheck {
  Runway = 1,
  NetFlow = 2,
  WipLoad = 3,
  Stale = 4,
  OldProposed = 5,
  AgingWip = 6,
  MissingStoryPoints = 7,
  Oversized = 8,
  NoParent = 9,
  NoProject = 10,
  UnassignedActive = 11,
  CarryOver = 12,
  ClosedParent = 13,
  RankInversion = 14,
}

/** Mirrors the API's BacklogHealthOutcome enum. */
export enum BacklogHealthOutcome {
  Assessed = 1,
  NotEnoughHistory = 2,
  NotApplicable = 3,
}

/** Checks that measure the whole backlog rather than flag work items. */
export const MEASURE_CHECKS = [
  BacklogHealthCheck.Runway,
  BacklogHealthCheck.NetFlow,
  BacklogHealthCheck.WipLoad,
]

export const isMeasureCheck = (check: BacklogHealthCheckDto) =>
  MEASURE_CHECKS.includes(check.check.id as BacklogHealthCheck)

const round = (value: number, digits: number) =>
  Number(value.toFixed(digits)).toString()

/** The check's result in words, or why it has none. */
export const formatCheckValue = (check: BacklogHealthCheckDto): string => {
  if (check.outcome.id === BacklogHealthOutcome.NotEnoughHistory)
    return 'Not enough history'
  if (check.outcome.id === BacklogHealthOutcome.NotApplicable)
    return 'Not applicable'

  switch (check.check.id as BacklogHealthCheck) {
    case BacklogHealthCheck.Runway:
      return `${round(check.value ?? 0, 1)} weeks`
    case BacklogHealthCheck.NetFlow:
      return check.value === undefined
        ? 'Nothing completed'
        : `${round(check.value, 2)} created per completed`
    case BacklogHealthCheck.WipLoad:
      return `${round(check.value ?? 0, 1)} per member`
    default:
      return `${check.flagged ?? 0} of ${check.inScope ?? 0} (${round(check.value ?? 0, 0)}%)`
  }
}

/**
 * What a check looks for and where it draws the line, in the values the
 * report was graded with — so a changed threshold shows in its explanation.
 */
export const describeCheck = (
  check: BacklogHealthCheck,
  health: TeamBacklogHealthDto,
): string => {
  const t = health.thresholds
  const window = `the top ${health.readinessWindowWorkItems} ranked work items`
  const byShare = `At Risk at ${t.atRiskPercent}% flagged, Unhealthy at ${t.unhealthyPercent}%.`
  const needsHistory = 'Needs at least 10 completed work items in the history.'

  switch (check) {
    case BacklogHealthCheck.Runway:
      return `Weeks the backlog lasts at the team's throughput over the last ${health.lookbackDays} days. Unhealthy below ${t.runwayUnhealthyWeeks} weeks; At Risk below ${t.runwayAtRiskWeeks} or above ${t.runwayTooLongWeeks} weeks.`
    case BacklogHealthCheck.NetFlow:
      return `Work items created per work item completed over the last ${health.lookbackDays} days. At Risk above ${t.netFlowAtRisk}, Unhealthy above ${t.netFlowUnhealthy}.`
    case BacklogHealthCheck.WipLoad:
      return `Active work items per team member${health.memberCount ? ` (${health.memberCount} members)` : ''}. At Risk above ${t.wipLoadAtRisk}, Unhealthy above ${t.wipLoadUnhealthy}.`
    case BacklogHealthCheck.Stale:
      return `Work items not modified in the last ${t.staleDays} days. ${byShare}`
    case BacklogHealthCheck.OldProposed:
      return `Proposed work items created more than ${t.oldProposedDays} days ago. ${byShare}`
    case BacklogHealthCheck.AgingWip:
      return health.agingWipDays === undefined
        ? `Active work items open longer than the team's ${t.agingWipPercentile}th percentile cycle time. ${needsHistory}`
        : `Active work items open longer than ${round(health.agingWipDays, 1)} days, the team's ${t.agingWipPercentile}th percentile cycle time. ${byShare}`
    case BacklogHealthCheck.MissingStoryPoints:
      return `Work items among ${window} without an estimate. ${byShare}`
    case BacklogHealthCheck.Oversized:
      return health.oversizedStoryPoints === undefined
        ? `Work items among ${window} estimated above the team's ${t.oversizedPercentile}th percentile. ${needsHistory}`
        : `Work items among ${window} estimated above ${health.oversizedStoryPoints} points, the team's ${t.oversizedPercentile}th percentile. ${byShare}`
    case BacklogHealthCheck.NoParent:
      return `Work items among ${window} without a parent. ${byShare}`
    case BacklogHealthCheck.NoProject:
      return `Work items among ${window} not linked to a project. Checked only when the team links work to projects. ${byShare}`
    case BacklogHealthCheck.UnassignedActive:
      return `Active work items with no one assigned. ${byShare}`
    case BacklogHealthCheck.CarryOver:
      return `Open work items still in a completed sprint. ${byShare}`
    case BacklogHealthCheck.ClosedParent:
      return `Open work items whose parent is done or removed. ${byShare}`
    case BacklogHealthCheck.RankInversion:
      return "Work items ranked above a work item they depend on in the team's backlog. Any inversion is Unhealthy."
  }
}

export const findCheck = (
  checks: BacklogHealthCheckDto[],
  check: BacklogHealthCheck,
) => checks.find((c) => c.check.id === check)
