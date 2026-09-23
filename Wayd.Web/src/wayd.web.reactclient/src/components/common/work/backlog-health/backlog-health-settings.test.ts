import { BacklogHealthThresholds } from '@/src/services/wayd-api'
import {
  EMPTY_BACKLOG_HEALTH_SETTINGS,
  hasOverrides,
  settingsFromSearchParams,
  thresholdConflicts,
  writeSettingsToSearchParams,
} from './backlog-health-settings'

describe('settingsFromSearchParams', () => {
  it('returns null when the link carries no report settings', () => {
    const params = new URLSearchParams('section=backlog-health')

    expect(settingsFromSearchParams(params)).toBeNull()
  })

  it('reads the lookback and each threshold the link carries', () => {
    const params = new URLSearchParams(
      'section=backlog-health&lookbackDays=60&staleDays=30&netFlowAtRisk=1.1',
    )

    expect(settingsFromSearchParams(params)).toEqual({
      lookbackDays: 60,
      thresholds: { staleDays: 30, netFlowAtRisk: 1.1 },
    })
  })

  it('ignores values that are not numbers', () => {
    const params = new URLSearchParams('staleDays=soon&oldProposedDays=')

    expect(settingsFromSearchParams(params)).toBeNull()
  })
})

describe('writeSettingsToSearchParams', () => {
  it('keeps unrelated parameters and replaces the report settings', () => {
    const params = new URLSearchParams(
      'section=backlog-health&staleDays=30&lookbackDays=60',
    )

    const next = writeSettingsToSearchParams(params, {
      thresholds: { oldProposedDays: 90 },
    })

    expect(next.get('section')).toBe('backlog-health')
    expect(next.get('staleDays')).toBeNull()
    expect(next.get('lookbackDays')).toBeNull()
    expect(next.get('oldProposedDays')).toBe('90')
  })

  it('round-trips through settingsFromSearchParams', () => {
    const settings = {
      lookbackDays: 120,
      thresholds: { wipLoadUnhealthy: 2.5, atRiskPercent: 5 },
    }

    const params = writeSettingsToSearchParams(new URLSearchParams(), settings)

    expect(settingsFromSearchParams(params)).toEqual(settings)
  })

  it('removes every report setting when reset', () => {
    const params = new URLSearchParams('staleDays=30&section=backlog-health')

    const next = writeSettingsToSearchParams(
      params,
      EMPTY_BACKLOG_HEALTH_SETTINGS,
    )

    expect(next.toString()).toBe('section=backlog-health')
  })
})

describe('thresholdConflicts', () => {
  const valid: BacklogHealthThresholds = {
    staleDays: 90,
    oldProposedDays: 180,
    agingWipPercentile: 85,
    oversizedPercentile: 85,
    readinessWindowWeeks: 4,
    readinessFallbackItems: 20,
    atRiskPercent: 10,
    unhealthyPercent: 25,
    runwayAtRiskWeeks: 4,
    runwayUnhealthyWeeks: 2,
    runwayTooLongWeeks: 26,
    netFlowAtRisk: 1.2,
    netFlowUnhealthy: 1.5,
    wipLoadAtRisk: 1.5,
    wipLoadUnhealthy: 2,
  }

  it('finds none in the defaults', () => {
    expect(thresholdConflicts(valid)).toEqual([])
  })

  it('allows an Unhealthy limit equal to its At Risk limit', () => {
    expect(
      thresholdConflicts({ ...valid, atRiskPercent: 20, unhealthyPercent: 20 }),
    ).toEqual([])
  })

  it.each<[string, Partial<BacklogHealthThresholds>]>([
    ['percent', { atRiskPercent: 30, unhealthyPercent: 20 }],
    ['runway', { runwayAtRiskWeeks: 1, runwayUnhealthyWeeks: 2 }],
    ['runway too long', { runwayTooLongWeeks: 4 }],
    ['net flow', { netFlowUnhealthy: 1.1 }],
    ['WIP load', { wipLoadUnhealthy: 1 }],
  ])('reports a %s pair the API would reject', (_, change) => {
    expect(thresholdConflicts({ ...valid, ...change })).toHaveLength(1)
  })
})

describe('hasOverrides', () => {
  it('is false for the defaults', () => {
    expect(hasOverrides(EMPTY_BACKLOG_HEALTH_SETTINGS)).toBe(false)
  })

  it('is true when only the lookback changed', () => {
    expect(hasOverrides({ lookbackDays: 30, thresholds: {} })).toBe(true)
  })
})
