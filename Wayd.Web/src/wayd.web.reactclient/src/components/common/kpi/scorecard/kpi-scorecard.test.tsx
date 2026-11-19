import {
  KpiExpressedAs,
  KpiScorecardDto,
  KpiSource,
  KpiStatus,
  KpiSummaryDto,
  KpiTargetDirection,
  KpiTargetKind,
  KpiTrend,
  KpiWindowKind,
} from '@/src/services/api/wayd.schemas'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import KpiScorecard from './kpi-scorecard'

const mockScorecard = vi.fn()
const mockClosed = vi.fn()

vi.mock('@/src/store/features/common/kpis-api', () => ({
  useGetKpiScorecardQuery: (...args: unknown[]) => mockScorecard(...args),
  useGetClosedKpisQuery: (...args: unknown[]) => mockClosed(...args),
}))

vi.mock('@/src/components/common/charts', () => ({
  Line: () => <div data-testid="sparkline" />,
}))

vi.mock('./kpi-form', () => ({
  default: () => <div role="dialog" aria-label="KPI form" />,
}))

vi.mock('./kpi-details-drawer', () => ({
  default: ({ kpiId }: { kpiId: string }) => (
    <div role="dialog" aria-label={`KPI ${kpiId}`} />
  ),
}))

const subject = {
  subjectType: 'organization.team' as const,
  subjectId: 'team-1',
}

const kpi = (
  id: string,
  name: string,
  category?: { id: string; name: string; order: number },
): KpiSummaryDto => ({
  id,
  key: 1,
  name,
  category,
  source: KpiSource.Manual,
  expressedAs: KpiExpressedAs.Value,
  window: { kind: KpiWindowKind.PerPeriod },
  status: KpiStatus.Active,
  order: 0,
  goal: {
    direction: KpiTargetDirection.Increase,
    baselineValue: 10,
    targetKind: KpiTargetKind.Absolute,
    targetValue: 20,
    resolvedTargetValue: 20,
  },
  reading: { value: 12, trend: KpiTrend.NoData },
  recentValues: [],
})

const flow = { id: 'cat-flow', name: 'Flow', order: 0 }
const quality = { id: 'cat-quality', name: 'Quality', order: 1 }

const scorecard = (
  overrides: Partial<KpiScorecardDto> = {},
): KpiScorecardDto => ({
  subjectType: subject.subjectType,
  subjectId: subject.subjectId,
  subjectName: 'Cards',
  canManage: true,
  kpis: [
    kpi('k1', 'Throughput', flow),
    kpi('k2', 'Lead time', flow),
    kpi('k3', 'Escaped bugs', quality),
    kpi('k4', 'Morale'),
  ],
  ...overrides,
})

describe('KpiScorecard', () => {
  beforeEach(() => {
    mockScorecard.mockReset()
    mockClosed.mockReset()
    mockScorecard.mockReturnValue({ data: scorecard(), isLoading: false })
    mockClosed.mockReturnValue({ data: [], isLoading: false })
  })

  it('groups the KPIs by category in the order the server sends them, uncategorized last', () => {
    render(<KpiScorecard subject={subject} />)

    const groups = screen.getAllByRole('region')
    expect(groups.map((g) => g.getAttribute('aria-label'))).toEqual([
      'Flow',
      'Quality',
      'Uncategorized',
    ])
    expect(within(groups[0]).getByText('Throughput')).toBeInTheDocument()
    expect(within(groups[0]).getByText('Lead time')).toBeInTheDocument()
    expect(
      within(groups[0]).queryByText('Escaped bugs'),
    ).not.toBeInTheDocument()
    expect(within(groups[2]).getByText('Morale')).toBeInTheDocument()
  })

  it('offers Add KPI only to someone who may manage the record', () => {
    mockScorecard.mockReturnValue({
      data: scorecard({ canManage: false }),
      isLoading: false,
    })

    render(<KpiScorecard subject={subject} />)

    expect(
      screen.queryByRole('button', { name: 'Add KPI' }),
    ).not.toBeInTheDocument()
  })

  it('opens the add form from Add KPI', async () => {
    render(<KpiScorecard subject={subject} />)

    await userEvent.click(screen.getByRole('button', { name: 'Add KPI' }))

    expect(screen.getByRole('dialog', { name: 'KPI form' })).toBeInTheDocument()
  })

  it('opens a KPI when its card is clicked', async () => {
    render(<KpiScorecard subject={subject} />)

    await userEvent.click(screen.getByText('Escaped bugs'))

    expect(screen.getByRole('dialog', { name: 'KPI k3' })).toBeInTheDocument()
  })

  it('opens a KPI from the keyboard', async () => {
    render(<KpiScorecard subject={subject} />)

    screen.getByRole('button', { name: 'KPI 1: Lead time' }).focus()
    await userEvent.keyboard('{Enter}')

    expect(screen.getByRole('dialog', { name: 'KPI k2' })).toBeInTheDocument()
  })

  it('asks for closed KPIs only once they are switched on, and shows them instead', async () => {
    mockClosed.mockImplementation((args, options) => ({
      data: options?.skip ? undefined : [kpi('k9', 'Old metric')],
      isLoading: false,
    }))

    render(<KpiScorecard subject={subject} />)
    expect(mockClosed).toHaveBeenLastCalledWith(subject, { skip: true })

    await userEvent.click(screen.getByRole('switch'))

    expect(mockClosed).toHaveBeenLastCalledWith(subject, { skip: false })
    expect(screen.getByText('Old metric')).toBeInTheDocument()
    expect(screen.queryByText('Throughput')).not.toBeInTheDocument()
  })

  it('invites someone who may manage it to add the first KPI', () => {
    mockScorecard.mockReturnValue({
      data: scorecard({ kpis: [] }),
      isLoading: false,
    })

    render(<KpiScorecard subject={subject} />)

    expect(screen.getByText(/No KPIs yet. Add one/)).toBeInTheDocument()
  })

  it('offers a retry when the scorecard cannot be loaded', async () => {
    const refetch = vi.fn()
    mockScorecard.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: { status: 500 },
      refetch,
    })

    render(<KpiScorecard subject={subject} />)
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))

    expect(refetch).toHaveBeenCalled()
  })
})
