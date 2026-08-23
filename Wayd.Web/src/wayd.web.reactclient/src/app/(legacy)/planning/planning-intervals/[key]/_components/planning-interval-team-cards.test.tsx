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
  useGetPlanningIntervalMetricsQuery: jest.fn(),
  useGetPlanningIntervalObjectivesQuery: jest.fn(),
}))

import { useGetPlanningIntervalMetricsQuery } from '@/src/store/features/planning/planning-interval-api'
import { useGetPlanningIntervalObjectivesQuery } from '@/src/store/features/planning/planning-interval-api'
import PlanningIntervalTeamCards from './planning-interval-team-cards'
jest.mock('../../_components', () => ({
  ObjectiveStatusChart: () => <div data-testid="objective-status-chart" />,
  ObjectiveHealthChart: () => <div data-testid="objective-health-chart" />,
}))

const mockMetrics = useGetPlanningIntervalMetricsQuery as unknown as jest.Mock
const mockObjectives =
  useGetPlanningIntervalObjectivesQuery as unknown as jest.Mock

const teamMetrics = (
  overrides: Partial<{
    id: string
    key: number
    name: string
    teamCode: string
    predictability: number | null
    averageCycleTimeDays: number | null
    sprintCount: number
    regularObjectivesCount: number
    stretchObjectivesCount: number
    completedObjectivesCount: number
  }> = {},
) => {
  const avg =
    overrides.averageCycleTimeDays === undefined
      ? 4.2
      : overrides.averageCycleTimeDays
  return {
    team: {
      id: overrides.id ?? 't1',
      key: overrides.key ?? 1,
      name: overrides.name ?? 'Team Alpha',
    },
    teamCode: overrides.teamCode ?? 'ALPHA',
    predictability:
      overrides.predictability === undefined ? 75 : overrides.predictability,
    regularObjectivesCount: overrides.regularObjectivesCount ?? 1,
    stretchObjectivesCount: overrides.stretchObjectivesCount ?? 0,
    completedObjectivesCount: overrides.completedObjectivesCount ?? 0,
    cycleTime: {
      // Use a notional sample count > 0 when avg is non-null so the data
      // shape is realistic; null avg → empty summary.
      workItemsCount: avg == null ? 0 : 4,
      totalCycleTimeDays: avg == null ? 0 : avg * 4,
      averageCycleTimeDays: avg,
    },
    sprintCount: overrides.sprintCount ?? 6,
  }
}

const findTeamLink = (name: string) =>
  screen.getByText(name).closest('a') as HTMLAnchorElement

describe('PlanningIntervalTeamCards', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockObjectives.mockReturnValue({ data: undefined })
  })

  it('renders a loading skeleton while the query is pending', () => {
    mockMetrics.mockReturnValue({ data: undefined, isLoading: true })

    const { container } = render(<PlanningIntervalTeamCards piKey={1} />)

    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders nothing when the team metrics list is empty', () => {
    mockMetrics.mockReturnValue({
      data: { teamMetrics: [] },
      isLoading: false,
    })

    const { container } = render(<PlanningIntervalTeamCards piKey={1} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders one card per team with name, predictability, and cycle time', () => {
    mockMetrics.mockReturnValue({
      data: {
        teamMetrics: [
          teamMetrics({
            id: 't1',
            key: 1,
            name: 'Alpha',
            teamCode: 'ALPHA',
            predictability: 80,
            averageCycleTimeDays: 3.1,
          }),
          teamMetrics({
            id: 't2',
            key: 2,
            name: 'Beta',
            teamCode: 'BETA',
            predictability: 50,
            averageCycleTimeDays: 5.6,
          }),
        ],
      },
      isLoading: false,
    })

    render(<PlanningIntervalTeamCards piKey={7} />)

    const alphaLink = findTeamLink('Alpha')
    expect(alphaLink).toHaveAttribute(
      'href',
      '/planning/planning-intervals/7/plan-review#alpha',
    )
    expect(alphaLink.textContent).toMatch(/80\s*%/)
    expect(alphaLink.textContent).toMatch(/3\.10\s*days/)

    const betaLink = findTeamLink('Beta')
    expect(betaLink).toHaveAttribute(
      'href',
      '/planning/planning-intervals/7/plan-review#beta',
    )
    expect(betaLink.textContent).toMatch(/50\s*%/)
    expect(betaLink.textContent).toMatch(/5\.60\s*days/)
  })

  it("renders the team's regular/stretch/completed objective counts", () => {
    mockMetrics.mockReturnValue({
      data: {
        teamMetrics: [
          teamMetrics({
            name: 'Alpha',
            regularObjectivesCount: 4,
            stretchObjectivesCount: 1,
            completedObjectivesCount: 3,
          }),
        ],
      },
      isLoading: false,
    })

    render(<PlanningIntervalTeamCards piKey={1} />)

    const link = findTeamLink('Alpha')

    expect(
      within(link).getByLabelText('Completed').nextSibling?.textContent,
    ).toBe('3')
    expect(
      within(link).getByLabelText('Regular').nextSibling?.textContent,
    ).toBe('4')
    expect(
      within(link).getByLabelText('Stretch').nextSibling?.textContent,
    ).toBe('1')
  })

  it('shows "N/A" when objective count is zero', () => {
    mockMetrics.mockReturnValue({
      data: {
        teamMetrics: [
          teamMetrics({
            name: 'Team Gamma',
            predictability: null,
            regularObjectivesCount: 0,
            stretchObjectivesCount: 0,
            averageCycleTimeDays: null,
          }),
        ],
      },
      isLoading: false,
    })

    render(<PlanningIntervalTeamCards piKey={1} />)

    const link = findTeamLink('Team Gamma')
    expect(link.textContent).toMatch(/N\/A/)
    expect(link.textContent).toMatch(/0\.00\s*days/)
    expect(within(link).queryByLabelText('Completed')).not.toBeInTheDocument()
    expect(within(link).queryByLabelText('Regular')).not.toBeInTheDocument()
    expect(within(link).queryByLabelText('Stretch')).not.toBeInTheDocument()
  })

  it('renders objective status and health charts when team objectives exist', () => {
    mockMetrics.mockReturnValue({
      data: { teamMetrics: [teamMetrics({ name: 'Alpha' })] },
      isLoading: false,
    })
    mockObjectives.mockReturnValue({
      data: [
        {
          id: 'o1',
          key: 1,
          name: 'Improve reliability',
          status: { id: 1, name: 'In Progress' },
          healthCheck: { id: 'h1', status: { id: 1, name: 'Healthy' } },
          planningInterval: { id: 'pi1', key: 1, name: 'PI 1' },
          team: { id: 't1', key: 1, name: 'Alpha', code: 'ALPHA' },
          progress: 50,
          type: { id: 1, name: 'Standard' },
          isStretch: false,
        },
      ],
    })

    render(<PlanningIntervalTeamCards piKey={1} />)

    expect(screen.getByTestId('objective-status-chart')).toBeInTheDocument()
    expect(screen.getByTestId('objective-health-chart')).toBeInTheDocument()
  })
})

