import {
  SprintMetricFiguresDto,
  SprintMetricSummariesDto,
  SprintMetricsExclusion,
  SprintMetricSummaryDto,
  TeamSprintMetricsDto,
  TeamSprintMetricsSprintDto,
} from '@/src/services/wayd-api'

/** The bounds the API holds a sprint window to. */
export const MIN_SPRINT_WINDOW = 3
export const MAX_SPRINT_WINDOW = 12

export type SprintMetricKind = 'velocity' | 'predictability' | 'sayDo'

/** What stands in for a metric a sprint has no value for. */
export const NO_VALUE = '—'

const amount = (value: number) =>
  value.toLocaleString(undefined, { maximumFractionDigits: 1 })

const percent = (value: number) => `${Math.round(value * 100)}%`

/** A metric's value as shown: an amount for velocity, a whole percentage for the ratios. */
export const formatSprintMetric = (
  kind: SprintMetricKind,
  value: number | null | undefined,
): string => {
  if (value === null || value === undefined) return NO_VALUE
  return kind === 'velocity' ? amount(value) : percent(value)
}

/** A metric's spread across the sprints, as its lowest and highest sprint. */
export const formatSprintMetricRange = (
  kind: SprintMetricKind,
  summary: SprintMetricSummaryDto | undefined,
): string | undefined =>
  summary
    ? `Range ${formatSprintMetric(kind, summary.min)}–${formatSprintMetric(kind, summary.max)}`
    : undefined

/** The summaries in the unit shown. */
export const sprintMetricSummaries = (
  metrics: TeamSprintMetricsDto,
  byCount: boolean,
): SprintMetricSummariesDto => (byCount ? metrics.byCount : metrics.byEstimate)

/**
 * A sprint's figures in the unit shown. Null by estimate for a sprint measured
 * in another sizing method than the report's: its estimates are in a different
 * unit, so they are neither shown beside the others nor averaged with them.
 */
export const sprintMetricFigures = (
  sprint: TeamSprintMetricsSprintDto,
  byCount: boolean,
): SprintMetricFiguresDto | null => {
  if (byCount) return sprint.byCount
  return sprint.inEstimateAverages ? sprint.byEstimate : null
}

export const EXCLUSION_LABELS: Record<SprintMetricsExclusion, string> = {
  [SprintMetricsExclusion.NonStandard]: 'Non-standard sprint',
  [SprintMetricsExclusion.HistoryIncomplete]: 'History incomplete',
  [SprintMetricsExclusion.NoCommitment]: 'Nothing committed',
}

export const EXCLUSION_DESCRIPTIONS: Record<SprintMetricsExclusion, string> = {
  [SprintMetricsExclusion.NonStandard]:
    "A non-standard sprint, such as an innovation and planning sprint, isn't comparable with the team's other sprints.",
  [SprintMetricsExclusion.HistoryIncomplete]:
    "Work item history has not been read in full for every workspace holding this sprint's work. A full sync of the connection completes it.",
  [SprintMetricsExclusion.NoCommitment]:
    'Nothing was in the sprint at its commitment point, so it has no predictability or say/do.',
}

export const METRIC_TOOLTIPS: Record<SprintMetricKind, string> = {
  velocity:
    'What the team completed while it was in each sprint, including work completed as Removed, averaged across the sprints.',
  predictability:
    "Velocity ÷ what was committed at each sprint's commitment point, up to 100%, averaged across the sprints. Finished work added after the commitment point counts toward it.",
  sayDo:
    'Of the work in each sprint at its commitment point, the share completed by its end, averaged across the sprints. Work added later does not count.',
}
