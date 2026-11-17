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

const subject = {
  subjectType: 'organization.team' as const,
  subjectId: 'team-1',
}

describe('KpiDetailsDrawer', () => {
  beforeEach(() => {
    mockGetKpi.mockReset()
  })

  it('offers a retry, not an endless skeleton, when the KPI cannot be loaded', async () => {
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

    expect(
      screen.getByText(
        'The KPI could not be loaded. It may have been removed.',
      ),
    ).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(refetch).toHaveBeenCalled()
  })
})
