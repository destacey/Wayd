import {
  KpiExpressedAs,
  KpiHealth,
  KpiSource,
  KpiStatus,
  KpiSummaryDto,
  KpiTargetDirection,
  KpiTargetKind,
  KpiTrend,
  KpiWindowKind,
} from '@/src/services/api/wayd.schemas'
import { asCalendarDate } from '@/src/utils/calendar-date'
import { render, screen } from '@testing-library/react'
import ScorecardKpiCard from './scorecard-kpi-card'

vi.mock('@/src/components/common/charts', () => ({
  Line: () => <div data-testid="sparkline" />,
}))

const q1 = {
  date: asCalendarDate('2026-03-31'),
  label: 'Q1',
  plannedValue: 12,
}

const kpi = (overrides: Partial<KpiSummaryDto> = {}): KpiSummaryDto => ({
  id: 'kpi-1',
  key: 1,
  name: 'Throughput',
  suffix: 'items',
  source: KpiSource.Manual,
  expressedAs: KpiExpressedAs.Value,
  window: { kind: KpiWindowKind.PerPeriod },
  checkpoints: [q1],
  status: KpiStatus.Active,
  order: 0,
  goal: {
    direction: KpiTargetDirection.Increase,
    baselineValue: 10,
    targetKind: KpiTargetKind.Absolute,
    targetValue: 20,
    resolvedTargetValue: 20,
  },
  reading: { value: 12, trend: KpiTrend.NoData, health: KpiHealth.Healthy },
  recentValues: [],
  ...overrides,
})

describe('ScorecardKpiCard', () => {
  it('shows the checkpoint the latest value was judged against', () => {
    // Arrange
    const judged = kpi({
      reading: {
        value: 12,
        trend: KpiTrend.NoData,
        health: KpiHealth.Healthy,
        checkpoint: q1,
      },
    })

    // Act
    render(<ScorecardKpiCard kpi={judged} onOpen={vi.fn()} />)

    // Assert
    expect(screen.getByText(/^Q1 plan/)).toBeInTheDocument()
  })

  it('shows no checkpoint when the latest value was judged against the target', () => {
    // Act
    render(<ScorecardKpiCard kpi={kpi()} onOpen={vi.fn()} />)

    // Assert
    expect(screen.queryByText(/plan/)).not.toBeInTheDocument()
    expect(screen.getByText(/^Target/)).toBeInTheDocument()
  })
})
