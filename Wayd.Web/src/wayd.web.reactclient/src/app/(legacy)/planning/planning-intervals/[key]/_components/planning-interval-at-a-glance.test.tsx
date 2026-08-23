import { render, screen, within } from '@testing-library/react'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: jest.fn().mockImplementation((query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: jest.fn(),
    removeListener: jest.fn(),
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
    dispatchEvent: jest.fn(),
  })),
})

jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalPredictabilityQuery: jest.fn(),
  useGetPlanningIntervalTeamsQuery: jest.fn(),
  useGetPlanningIntervalMetricsQuery: jest.fn(),
  useGetPlanningIntervalObjectivesQuery: jest.fn(),
}))

// Heavy chart components are tested separately. We just want to assert that
// they're rendered (or not) under the right conditions.
jest.mock('../../_components', () => ({
  TeamPredictabilityRadarChart: () => (
    <div data-testid="team-predictability-radar" />
  ),
  ObjectiveStatusChart: () => <div data-testid="objective-status-chart" />,
  ObjectiveHealthChart: () => <div data-testid="objective-health-chart" />,
}))

// TimelineProgress already has its own tests, avoid pulling dayjs / Antd Grid
// dependencies into this suite.
jest.mock('@/src/components/common/planning/timeline-progress', () => ({
  __esModule: true,
  default: () => <div data-testid="timeline-progress" />,
}))

import {
  useGetPlanningIntervalMetricsQuery,
  useGetPlanningIntervalObjectivesQuery,
  useGetPlanningIntervalPredictabilityQuery,
  useGetPlanningIntervalTeamsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import PlanningIntervalAtAGlance from './planning-interval-at-a-glance'

const mockPredictability =
  useGetPlanningIntervalPredictabilityQuery as unknown as jest.Mock
const mockTeams = useGetPlanningIntervalTeamsQuery as unknown as jest.Mock
const mockMetrics = useGetPlanningIntervalMetricsQuery as unknown as jest.Mock
const mockObjectives =
  useGetPlanningIntervalObjectivesQuery as unknown as jest.Mock

// IterationState enum values: Unknown=0, Completed=1, Active=2, Future=3
const ACTIVE = 2
const FUTURE = 3

const mkPi = (
  overrides: Partial<{ stateId: number; predictability: number }> = {},
) => ({
  id: 'pi-1',
  key: 7,
  name: '2026 PI 1',
  start: new Date('2026-01-01') as unknown as Date,
  end: new Date('2026-04-01') as unknown as Date,
  state: { id: overrides.stateId ?? ACTIVE, name: 'Active' },
  predictability: overrides.predictability ?? 0,
  // unused fields for the component
  description: '',
  objectivesLocked: false,
})

const mkObjective = (
  overrides: Partial<{ status: string; isStretch: boolean }> = {},
) => ({
  id: 'obj-' + Math.random(),
  key: 1,
  name: 'Obj',
  status: { id: 1, name: overrides.status ?? 'Not Started' },
  isStretch: overrides.isStretch ?? false,
  type: { id: 1, name: 'Business' },
})

describe('PlanningIntervalAtAGlance', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockPredictability.mockReturnValue({ data: undefined, isLoading: false })
    mockTeams.mockReturnValue({ data: undefined })
    mockMetrics.mockReturnValue({ data: undefined })
    mockObjectives.mockReturnValue({ data: undefined })
  })

  it('renders the timeline and a Teams card even with no other data', () => {
    render(<PlanningIntervalAtAGlance planningInterval={mkPi() as any} />)

    expect(screen.getByTestId('timeline-progress')).toBeInTheDocument()
    expect(screen.getByText('Teams')).toBeInTheDocument()
  })

  it('counts only teams with type === "Team" for the Teams metric', () => {
    mockTeams.mockReturnValue({
      data: [
        {
          id: 't1',
          key: 1,
          name: 'Alpha',
          code: 'A',
          type: 'Team',
          isActive: true,
        },
        {
          id: 't2',
          key: 2,
          name: 'Beta',
          code: 'B',
          type: 'Team',
          isActive: true,
        },
        {
          id: 'tot',
          key: 3,
          name: 'Org',
          code: 'O',
          type: 'Team of Teams',
          isActive: true,
        },
      ],
    })

    render(<PlanningIntervalAtAGlance planningInterval={mkPi() as any} />)

    // Find the Teams metric card by its title, then read the value within
    const teamsTitle = screen.getByText('Teams')
    const card = teamsTitle.closest('.ant-card')!
    expect(within(card as HTMLElement).getByText('2')).toBeInTheDocument()
  })

  it('skips the metrics query for Future PIs', () => {
    render(
      <PlanningIntervalAtAGlance
        planningInterval={mkPi({ stateId: FUTURE }) as any}
      />,
    )

    expect(mockMetrics).toHaveBeenCalled()
    const args = mockMetrics.mock.calls[mockMetrics.mock.calls.length - 1]
    expect(args[1]).toEqual({ skip: true })
  })

  it('renders an Objectives metric for Future PIs with regular and stretch counts', () => {
    mockObjectives.mockReturnValue({
      data: [
        mkObjective({ isStretch: false }),
        mkObjective({ isStretch: false }),
        mkObjective({ isStretch: true }),
      ],
    })

    render(
      <PlanningIntervalAtAGlance
        planningInterval={mkPi({ stateId: FUTURE }) as any}
      />,
    )

    const objectivesCard = screen.getByText('Objectives').closest('.ant-card')!
    expect(within(objectivesCard as HTMLElement).getByText('3')).toBeInTheDocument()
    expect(
      within(objectivesCard as HTMLElement).getByLabelText('Regular')
        .nextSibling?.textContent,
    ).toBe('2')
    expect(
      within(objectivesCard as HTMLElement).getByLabelText('Stretch')
        .nextSibling?.textContent,
    ).toBe('1')
    expect(screen.queryByText('PI Predictability')).not.toBeInTheDocument()
  })

  it('runs the metrics query for Active PIs', () => {
    render(
      <PlanningIntervalAtAGlance
        planningInterval={mkPi({ stateId: ACTIVE }) as any}
      />,
    )

    const args = mockMetrics.mock.calls[mockMetrics.mock.calls.length - 1]
    expect(args[1]).toEqual({ skip: false })
  })

  it('only renders the Cycle Time card when there are contributing work items', () => {
    const { rerender } = render(
      <PlanningIntervalAtAGlance planningInterval={mkPi() as any} />,
    )
    expect(screen.queryByText('Avg Cycle Time')).not.toBeInTheDocument()

    // Empty cycle-time summary should still be hidden — count is the gate.
    mockMetrics.mockReturnValue({
      data: {
        cycleTime: {
          workItemsCount: 0,
          totalCycleTimeDays: 0,
          averageCycleTimeDays: null,
        },
      },
    })
    rerender(<PlanningIntervalAtAGlance planningInterval={mkPi() as any} />)
    expect(screen.queryByText('Avg Cycle Time')).not.toBeInTheDocument()

    // Non-zero count → card shows.
    mockMetrics.mockReturnValue({
      data: {
        cycleTime: {
          workItemsCount: 4,
          totalCycleTimeDays: 18,
          averageCycleTimeDays: 4.5,
        },
      },
    })
    rerender(<PlanningIntervalAtAGlance planningInterval={mkPi() as any} />)
    expect(screen.getByText('Avg Cycle Time')).toBeInTheDocument()
  })

  describe('predictability secondary stats', () => {
    beforeEach(() => {
      mockObjectives.mockReturnValue({
        data: [
          mkObjective({ status: 'Completed', isStretch: false }),
          mkObjective({ status: 'In Progress', isStretch: false }),
          mkObjective({ status: 'Not Started', isStretch: false }),
          mkObjective({ status: 'Completed', isStretch: true }),
          mkObjective({ status: 'Not Started', isStretch: true }),
        ],
      })
    })

    it('renders the PI Predictability card', () => {
      render(<PlanningIntervalAtAGlance planningInterval={mkPi() as any} />)
      expect(screen.getByText('PI Predictability')).toBeInTheDocument()
    })

    it('counts completed, regular (non-stretch), and stretch objectives separately', () => {
      render(<PlanningIntervalAtAGlance planningInterval={mkPi() as any} />)

      const predictabilityCard = screen
        .getByText('PI Predictability')
        .closest('.ant-card')!

      // 2 completed total (one regular, one stretch)
      expect(
        within(predictabilityCard as HTMLElement).getByLabelText('Completed')
          .nextSibling?.textContent,
      ).toBe('2')
      // 3 regular (non-stretch) objectives
      expect(
        within(predictabilityCard as HTMLElement).getByLabelText('Regular')
          .nextSibling?.textContent,
      ).toBe('3')
      // 2 stretch objectives
      expect(
        within(predictabilityCard as HTMLElement).getByLabelText('Stretch')
          .nextSibling?.textContent,
      ).toBe('2')
    })
  })

  it('renders the chart row only when objectives exist', () => {
    const { rerender } = render(
      <PlanningIntervalAtAGlance planningInterval={mkPi() as any} />,
    )
    expect(
      screen.queryByTestId('team-predictability-radar'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByTestId('objective-status-chart'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByTestId('objective-health-chart'),
    ).not.toBeInTheDocument()

    mockObjectives.mockReturnValue({
      data: [mkObjective({ status: 'Completed' })],
    })
    rerender(<PlanningIntervalAtAGlance planningInterval={mkPi() as any} />)

    expect(screen.getByTestId('team-predictability-radar')).toBeInTheDocument()
    expect(screen.getByTestId('objective-status-chart')).toBeInTheDocument()
    expect(screen.getByTestId('objective-health-chart')).toBeInTheDocument()
  })

  it('does not render chart row for Future PIs even when objectives exist', () => {
    mockObjectives.mockReturnValue({
      data: [mkObjective({ status: 'Completed' })],
    })

    render(
      <PlanningIntervalAtAGlance
        planningInterval={mkPi({ stateId: FUTURE }) as any}
      />,
    )

    expect(
      screen.queryByTestId('team-predictability-radar'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByTestId('objective-status-chart'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByTestId('objective-health-chart'),
    ).not.toBeInTheDocument()
  })
})

