import {
  EMPTY_BACKLOG_HEALTH_SETTINGS,
  hasOverrides,
  settingsFromSearchParams,
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

describe('hasOverrides', () => {
  it('is false for the defaults', () => {
    expect(hasOverrides(EMPTY_BACKLOG_HEALTH_SETTINGS)).toBe(false)
  })

  it('is true when only the lookback changed', () => {
    expect(hasOverrides({ lookbackDays: 30, thresholds: {} })).toBe(true)
  })
})
