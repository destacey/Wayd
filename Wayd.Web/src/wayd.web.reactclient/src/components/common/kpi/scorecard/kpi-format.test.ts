import {
  KpiAtRiskKind,
  KpiExpressedAs,
  KpiGoalDto,
  KpiSource,
  KpiStatus,
  KpiSummaryDto,
  KpiTargetDirection,
  KpiTargetKind,
  KpiTrend,
} from '@/src/services/api/wayd.schemas'
import {
  atRiskLabel,
  describeAtRisk,
  describeChange,
  describeTarget,
  formatKpiValue,
  kpiHeadline,
} from './kpi-format'

const goal = (overrides: Partial<KpiGoalDto> = {}): KpiGoalDto => ({
  direction: KpiTargetDirection.Decrease,
  baselineValue: 20,
  targetKind: KpiTargetKind.Absolute,
  targetValue: 15,
  resolvedTargetValue: 15,
  ...overrides,
})

const summary = (overrides: Partial<KpiSummaryDto> = {}): KpiSummaryDto => ({
  id: 'kpi-1',
  key: 7,
  name: 'Cycle time',
  suffix: 'days',
  source: KpiSource.Manual,
  expressedAs: KpiExpressedAs.Value,
  status: KpiStatus.Active,
  order: 0,
  goal: goal(),
  reading: {
    value: 16,
    changeFromBaseline: -4,
    changeFromBaselinePercent: -20,
    progressToTarget: 0.8,
    trend: KpiTrend.Improving,
  },
  recentValues: [],
  ...overrides,
})

describe('formatKpiValue', () => {
  it('puts the prefix before and the suffix after the value', () => {
    expect(formatKpiValue(1234.5, '$', 'k')).toBe(
      `$${(1234.5).toLocaleString()} k`,
    )
  })

  it('shows a dash for a missing value', () => {
    expect(formatKpiValue(undefined, '$')).toBe('—')
  })

  it('puts the sign before the prefix', () => {
    expect(formatKpiValue(-5, '$')).toBe('-$5')
  })

  it('never signs a value that rounds to zero', () => {
    expect(formatKpiValue(-0.001, '$')).toBe('$0')
  })

  it('sets a symbol suffix against the number', () => {
    expect(formatKpiValue(18, undefined, '%')).toBe('18%')
  })
})

describe('describeChange', () => {
  it('signs a rise and adds its percentage', () => {
    expect(describeChange(4, 25, undefined, 'days')).toBe('+4 days (+25%)')
  })

  it('leaves the percentage out when there is none', () => {
    expect(describeChange(-3, undefined, '$')).toBe('-$3')
  })
})

describe('atRiskLabel', () => {
  it('reads below when higher is better and above when lower is', () => {
    expect(atRiskLabel(KpiTargetDirection.Increase)).toBe('At risk below')
    expect(atRiskLabel(KpiTargetDirection.Decrease)).toBe('At risk above')
  })
})

describe('kpiHeadline', () => {
  it('leads with the raw value for a KPI shown as its value', () => {
    expect(kpiHeadline(summary())).toEqual({ label: 'Latest', text: '16 days' })
  })

  it('leads with the signed change for a KPI shown as change from baseline', () => {
    const headline = kpiHeadline(
      summary({
        expressedAs: KpiExpressedAs.ChangeFromBaseline,
        reading: { ...summary().reading, changeFromBaselinePercent: 12.5 },
      }),
    )

    expect(headline).toEqual({ label: 'Change from baseline', text: '+12.5%' })
  })

  it('leads with progress as a percentage for a KPI shown as progress', () => {
    const headline = kpiHeadline(
      summary({ expressedAs: KpiExpressedAs.ProgressToTarget }),
    )

    expect(headline).toEqual({ label: 'Progress to target', text: '80%' })
  })

  it('falls back to the raw value when the chosen reading cannot be worked out', () => {
    const headline = kpiHeadline(
      summary({
        expressedAs: KpiExpressedAs.ProgressToTarget,
        reading: { ...summary().reading, progressToTarget: undefined },
      }),
    )

    expect(headline).toEqual({ label: 'Latest', text: '16 days' })
  })

  it('shows a dash before anything is measured, whatever the reading', () => {
    const headline = kpiHeadline(
      summary({
        expressedAs: KpiExpressedAs.ChangeFromBaseline,
        reading: { trend: KpiTrend.NoData },
      }),
    )

    expect(headline).toEqual({ label: 'Latest', text: '—' })
  })
})

describe('describeTarget', () => {
  it('names an absolute target as its value', () => {
    expect(describeTarget(goal(), undefined, 'days')).toBe('15 days')
  })

  it('names a relative target by its signed change and what it resolves to', () => {
    const relative = goal({
      targetKind: KpiTargetKind.RelativeChange,
      targetValue: 25,
      resolvedTargetValue: 15,
    })

    expect(describeTarget(relative, undefined, 'days')).toBe(
      '−25% from baseline (15 days)',
    )
  })
})

describe('describeAtRisk', () => {
  it('is null when the KPI has no at-risk threshold', () => {
    expect(describeAtRisk(goal())).toBeNull()
  })

  it('names a threshold stated as progress with what it resolves to', () => {
    const atRisk = goal({
      atRiskKind: KpiAtRiskKind.ProgressToTarget,
      atRiskValue: 60,
      resolvedAtRiskValue: 17,
    })

    expect(describeAtRisk(atRisk, undefined, 'days')).toBe(
      '60% of the way to target (17 days)',
    )
  })
})
