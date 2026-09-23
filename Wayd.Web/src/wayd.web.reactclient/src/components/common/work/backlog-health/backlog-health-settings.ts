import { BacklogHealthThresholds } from '@/src/services/wayd-api'

/**
 * What the viewer changed from the API's defaults. Only overrides are held:
 * the defaults live on the server, which reports the values it used.
 */
export interface BacklogHealthSettings {
  lookbackDays?: number
  thresholds: Partial<BacklogHealthThresholds>
}

export const EMPTY_BACKLOG_HEALTH_SETTINGS: BacklogHealthSettings = {
  thresholds: {},
}

export type ThresholdKey = keyof BacklogHealthThresholds

export interface ThresholdField {
  key: ThresholdKey
  label: string
  suffix?: string
  min: number
  max: number
  step: number
}

export interface ThresholdGroup {
  title: string
  fields: ThresholdField[]
}

// Limits mirror BacklogHealthThresholdsValidator, so an out-of-range value is
// stopped at the input instead of coming back as a 422.
export const THRESHOLD_GROUPS: ThresholdGroup[] = [
  {
    title: 'Flagging work items',
    fields: [
      {
        key: 'staleDays',
        label: 'Stale after',
        suffix: 'days',
        min: 1,
        max: 3650,
        step: 1,
      },
      {
        key: 'oldProposedDays',
        label: 'Old proposed after',
        suffix: 'days',
        min: 1,
        max: 3650,
        step: 1,
      },
      {
        key: 'agingWipPercentile',
        label: 'Aging WIP beyond',
        suffix: 'th pct',
        min: 1,
        max: 100,
        step: 1,
      },
      {
        key: 'oversizedPercentile',
        label: 'Oversized above',
        suffix: 'th pct',
        min: 1,
        max: 100,
        step: 1,
      },
      {
        key: 'readinessWindowWeeks',
        label: 'Readiness window',
        suffix: 'weeks',
        min: 1,
        max: 52,
        step: 1,
      },
      {
        key: 'readinessFallbackItems',
        label: 'Readiness window without history',
        suffix: 'items',
        min: 1,
        max: 1000,
        step: 1,
      },
    ],
  },
  {
    title: 'Grading work item checks',
    fields: [
      {
        key: 'atRiskPercent',
        label: 'At Risk at',
        suffix: '% flagged',
        min: 1,
        max: 100,
        step: 1,
      },
      {
        key: 'unhealthyPercent',
        label: 'Unhealthy at',
        suffix: '% flagged',
        min: 1,
        max: 100,
        step: 1,
      },
    ],
  },
  {
    title: 'Grading backlog measures',
    fields: [
      {
        key: 'runwayAtRiskWeeks',
        label: 'Runway At Risk below',
        suffix: 'weeks',
        min: 0,
        max: 520,
        step: 0.5,
      },
      {
        key: 'runwayUnhealthyWeeks',
        label: 'Runway Unhealthy below',
        suffix: 'weeks',
        min: 0,
        max: 520,
        step: 0.5,
      },
      {
        key: 'runwayTooLongWeeks',
        label: 'Runway At Risk above',
        suffix: 'weeks',
        min: 1,
        max: 520,
        step: 1,
      },
      {
        key: 'netFlowAtRisk',
        label: 'Net flow At Risk above',
        min: 0.01,
        max: 100,
        step: 0.1,
      },
      {
        key: 'netFlowUnhealthy',
        label: 'Net flow Unhealthy above',
        min: 0.01,
        max: 100,
        step: 0.1,
      },
      {
        key: 'wipLoadAtRisk',
        label: 'WIP load At Risk above',
        suffix: 'per member',
        min: 0.01,
        max: 100,
        step: 0.1,
      },
      {
        key: 'wipLoadUnhealthy',
        label: 'WIP load Unhealthy above',
        suffix: 'per member',
        min: 0.01,
        max: 100,
        step: 0.1,
      },
    ],
  },
]

const THRESHOLD_KEYS: ThresholdKey[] = THRESHOLD_GROUPS.flatMap((g) =>
  g.fields.map((f) => f.key),
)

const LOOKBACK_PARAM = 'lookbackDays'

export const hasOverrides = (settings: BacklogHealthSettings): boolean =>
  settings.lookbackDays !== undefined ||
  Object.keys(settings.thresholds).length > 0

const parseNumber = (value: string | null): number | undefined => {
  if (value === null || value.trim() === '') return undefined
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : undefined
}

/**
 * The settings a shared link carries, or null when it carries none — so a
 * plain link falls back to the viewer's own saved settings.
 */
export const settingsFromSearchParams = (
  params: URLSearchParams,
): BacklogHealthSettings | null => {
  const thresholds: Partial<BacklogHealthThresholds> = {}
  for (const key of THRESHOLD_KEYS) {
    const value = parseNumber(params.get(key))
    if (value !== undefined) thresholds[key] = value
  }
  const settings: BacklogHealthSettings = {
    lookbackDays: parseNumber(params.get(LOOKBACK_PARAM)),
    thresholds,
  }
  return hasOverrides(settings) ? settings : null
}

/**
 * Replaces the report's parameters in `params` with `settings`, leaving any
 * other parameter (such as the page's section) alone.
 */
export const writeSettingsToSearchParams = (
  params: URLSearchParams,
  settings: BacklogHealthSettings,
): URLSearchParams => {
  const next = new URLSearchParams(params)
  next.delete(LOOKBACK_PARAM)
  for (const key of THRESHOLD_KEYS) next.delete(key)

  if (settings.lookbackDays !== undefined)
    next.set(LOOKBACK_PARAM, String(settings.lookbackDays))
  for (const key of THRESHOLD_KEYS) {
    const value = settings.thresholds[key]
    if (value !== undefined) next.set(key, String(value))
  }
  return next
}
