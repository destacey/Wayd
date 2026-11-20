import {
  KpiDetailsDto,
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
import userEvent from '@testing-library/user-event'
import KpiDetailsDrawer from './kpi-details-drawer'

const mockGetKpi = vi.fn()

vi.mock('@/src/store/features/common/kpis-api', () => ({
  useGetKpiQuery: (...args: unknown[]) => mockGetKpi(...args),
  useCloseKpiMutation: () => [vi.fn()],
  useReopenKpiMutation: () => [vi.fn()],
  useRemoveKpiMutation: () => [vi.fn()],
  useRemoveKpiMeasurementMutation: () => [vi.fn()],
}))

vi.mock('@/src/components/common/charts', () => ({
  Line: () => <div data-testid="history-chart" />,
}))

// The actions as plain buttons, so a test reads which are offered without opening the menu.
vi.mock('@/src/components/common/page-actions', () => ({
  default: ({
    actionItems,
  }: {
    actionItems: { key?: string; label?: string }[]
  }) => (
    <div>
      {actionItems
        .filter((item) => item.label)
        .map((item) => (
          <button key={item.key}>{item.label}</button>
        ))}
    </div>
  ),
}))

const subject = {
  subjectType: 'organization.team' as const,
  subjectId: 'team-1',
}

const q1 = {
  date: asCalendarDate('2026-03-31'),
  label: 'Q1',
  plannedValue: 12,
  atRiskValue: 11,
}

const q2 = {
  date: asCalendarDate('2026-06-30'),
  label: 'Q2',
  plannedValue: 15,
}

const summary = (overrides: Partial<KpiSummaryDto> = {}): KpiSummaryDto => ({
  id: 'kpi-1',
  key: 1,
  name: 'Throughput',
  source: KpiSource.Manual,
  expressedAs: KpiExpressedAs.Value,
  window: { kind: KpiWindowKind.PerPeriod },
  checkpoints: [q1, q2],
  status: KpiStatus.Active,
  order: 0,
  goal: {
    direction: KpiTargetDirection.Increase,
    baselineValue: 10,
    targetKind: KpiTargetKind.Absolute,
    targetValue: 20,
    resolvedTargetValue: 20,
  },
  reading: {
    value: 12,
    trend: KpiTrend.NoData,
    health: KpiHealth.Healthy,
    checkpoint: q1,
  },
  recentValues: [],
  ...overrides,
})

const details = (overrides: Partial<KpiDetailsDto> = {}): KpiDetailsDto => ({
  kpi: summary(),
  subjectName: 'Cards',
  canManage: true,
  measurements: [],
  checkpointReadings: [
    {
      checkpoint: q1,
      measurement: {
        periodStart: asCalendarDate('2026-03-01'),
        periodEnd: asCalendarDate('2026-03-31'),
        value: 12,
      },
      reading: { value: 12, trend: KpiTrend.NoData, health: KpiHealth.Healthy },
    },
    {
      checkpoint: q2,
      reading: { trend: KpiTrend.NoData },
    },
  ],
  ...overrides,
})

const renderDrawer = (data: KpiDetailsDto) => {
  mockGetKpi.mockReturnValue({
    data,
    isLoading: false,
    error: undefined,
    refetch: vi.fn(),
  })
  render(<KpiDetailsDrawer subject={subject} kpiId="kpi-1" onClose={vi.fn()} />)
}

describe('KpiDetailsDrawer', () => {
  beforeEach(() => {
    mockGetKpi.mockReset()
  })

  it('offers a retry, not an endless skeleton, when the KPI cannot be loaded', async () => {
    // Arrange
    const refetch = vi.fn()
    mockGetKpi.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: { status: 404 },
      refetch,
    })
    render(
      <KpiDetailsDrawer subject={subject} kpiId="kpi-1" onClose={vi.fn()} />,
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))

    // Assert
    expect(
      screen.getByText(
        'The KPI could not be loaded. It may have been removed.',
      ),
    ).toBeInTheDocument()
    expect(refetch).toHaveBeenCalled()
  })

  it('says which checkpoint the latest value was judged against', () => {
    // Act
    renderDrawer(details())

    // Assert
    expect(screen.getByText('Judged against')).toBeInTheDocument()
    expect(screen.getByText(/Q1 plan:/)).toBeInTheDocument()
  })

  it('offers the checkpoint plan on an open KPI the viewer manages', () => {
    // Act
    renderDrawer(details())

    // Assert
    expect(
      screen.getByRole('button', { name: 'Checkpoint Plan' }),
    ).toBeInTheDocument()
  })

  it('does not offer the checkpoint plan on a closed KPI', () => {
    // Act
    renderDrawer(details({ kpi: summary({ status: KpiStatus.Closed }) }))

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Checkpoint Plan' }),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reopen' })).toBeInTheDocument()
  })

  it('offers no actions to a viewer who cannot manage the KPI', () => {
    // Act
    renderDrawer(details({ canManage: false }))

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Checkpoint Plan' }),
    ).not.toBeInTheDocument()
  })

  it('lists each checkpoint, marking one nothing was read against', () => {
    // Act
    renderDrawer(details())

    // Assert
    expect(screen.getByText('Q2')).toBeInTheDocument()
    expect(screen.getByText('Not measured')).toBeInTheDocument()
  })
})
