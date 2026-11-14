import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import MyTeamsWidget, { daysSince } from './my-teams-widget'
import {
  HomeContextResponse,
  Methodology,
  MyTeamSummaryResponse,
  MyTeamsSummaryResponse,
} from '@/src/services/wayd-api'
import { homeContext } from '../home-context-test-utils'
import { renderReportingWidget } from '../widget-display-test-utils'

const mockHome: { context?: HomeContextResponse } = {}

vi.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => true }),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/store/features/user-management/me-api', () => ({
  useGetMyTeamsSummaryQuery: vi.fn(),
}))

// The sprint card loads its own sprint, metrics and burn-up; these tests read only which teams get one.
vi.mock('@/src/components/common/planning/active-team-sprint', () => ({
  default: ({
    teamId,
    showBurnUp = true,
  }: {
    teamId: string
    showBurnUp?: boolean
  }) => (
    <div data-testid="active-sprint" data-burn-up={String(showBurnUp)}>
      {teamId}
    </div>
  ),
}))

import { useGetMyTeamsSummaryQuery } from '@/src/store/features/user-management/me-api'

const mockSummary = useGetMyTeamsSummaryQuery as Mock

const team = (
  id: string,
  overrides: Partial<MyTeamSummaryResponse> = {},
): MyTeamSummaryResponse => ({
  team: {
    id,
    key: 7,
    name: `Team ${id}`,
    code: id.toUpperCase(),
    type: 'Team',
  },
  dependencies: { openCount: 0, atRiskCount: 0, unhealthyCount: 0 },
  openRiskCount: 0,
  ...overrides,
})

const loaded = (summary: Partial<MyTeamsSummaryResponse>) => ({
  data: { teams: [], teamsOfTeams: [], dependenciesAtRiskCount: 0, ...summary },
  isLoading: false,
  isError: false,
})

describe('daysSince', () => {
  it('counts whole days, never below zero', () => {
    // Arrange
    const now = new Date('2026-11-09T12:00:00Z')

    // Act / Assert
    expect(daysSince(new Date('2026-11-06T13:00:00Z'), now)).toBe(2)
    expect(daysSince(new Date('2026-11-10T00:00:00Z'), now)).toBe(0)
  })
})

describe('MyTeamsWidget', () => {
  beforeEach(() => {
    mockHome.context = homeContext({ teams: { teamCount: 1 } })
  })

  it('draws the sprint card for a Scrum team with an active sprint', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teams: [
          team('a', {
            methodology: Methodology.Scrum,
            sprint: { activeSprint: { id: 's', key: 3, name: 'Sprint 3' } },
          }),
        ],
      }),
    )

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(screen.getByTestId('active-sprint')).toHaveTextContent('a')
  })

  it('draws the sprint card with its burn-up unless set to figures only', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teams: [
          team('a', {
            methodology: Methodology.Scrum,
            sprint: { activeSprint: { id: 's', key: 3, name: 'Sprint 3' } },
          }),
        ],
      }),
    )
    const { rerender } = render(<MyTeamsWidget title="My teams" />)
    expect(screen.getByTestId('active-sprint')).toHaveAttribute(
      'data-burn-up',
      'true',
    )

    // Act
    rerender(
      <MyTeamsWidget title="My teams" settings={{ sprintCard: 'figures' }} />,
    )

    // Assert
    expect(screen.getByTestId('active-sprint')).toHaveAttribute(
      'data-burn-up',
      'false',
    )
  })

  it('says a Scrum team between sprints has none running', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teams: [team('a', { methodology: Methodology.Scrum, sprint: {} })],
      }),
    )

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(screen.queryByTestId('active-sprint')).not.toBeInTheDocument()
    expect(screen.getByText(/No active sprint/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Team a' })).toHaveAttribute(
      'href',
      '/organizations/teams/7',
    )
  })

  it("shows a Kanban team's flow and the item in progress longest", () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teams: [
          team('a', {
            methodology: Methodology.Kanban,
            flow: {
              currentPeriod: { start: '2026-11-02', end: '2026-11-15' },
              inProgressCount: 4,
              completedSince: '2026-10-27',
              completedCount: 9,
              medianCycleTimeDays: 3.5,
              oldestInProgress: {
                id: 'w',
                key: 'WAYD-7',
                workspaceKey: 'WAYD',
                title: 'Long-running item',
                activatedOn: new Date('2026-10-01T00:00:00Z'),
              },
            },
          }),
        ],
      }),
    )

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(
      screen.getByText('Reporting period Nov 2 – Nov 15'),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'WAYD-7' })).toHaveAttribute(
      'href',
      '/work/workspaces/WAYD/work-items/WAYD-7',
    )
    expect(screen.getByText('Long-running item')).toBeInTheDocument()
  })

  it('links the footer to the team page’s dependencies and risks, with how many are at risk', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teams: [
          team('a', {
            dependencies: { openCount: 5, atRiskCount: 1, unhealthyCount: 2 },
            openRiskCount: 1,
          }),
        ],
      }),
    )

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(
      screen.getByRole('link', { name: /5 dependencies/ }),
    ).toHaveAttribute(
      'href',
      '/organizations/teams/7?section=dependency-management',
    )
    expect(screen.getByText('· 3 at risk')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '1 open risk' })).toHaveAttribute(
      'href',
      '/organizations/teams/7?section=risk-management',
    )
  })

  it('leaves out a footer item the caller may not see', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teams: [team('a', { dependencies: undefined, openRiskCount: 2 })],
      }),
    )

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(screen.queryByText(/dependenc/)).not.toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: '2 open risks' }),
    ).toBeInTheDocument()
  })

  it('links View all to the Teams list', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded({ teams: [team('a')] }))

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/organizations/teams',
    )
  })

  it('says how it fills for someone on no team, without asking', () => {
    // Arrange
    mockHome.context = homeContext()
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(
      screen.getByText(/You are not a member of a team/),
    ).toBeInTheDocument()
    expect(mockSummary).toHaveBeenCalledWith(undefined, { skip: true })
  })

  it('says when the teams could not be loaded', () => {
    // Arrange
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(screen.getByText('Could not load your teams.')).toBeInTheDocument()
  })

  describe('reported state', () => {
    it('reports loading while the summary loads', () => {
      // Arrange
      mockSummary.mockReturnValue({
        data: undefined,
        isLoading: true,
        isError: false,
      })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyTeamsWidget title="My teams" />,
      )

      // Assert
      expect(reportedState()).toBe('loading')
    })

    it('reports failed, though there is no data, when the query errors', () => {
      // Arrange
      mockSummary.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: true,
      })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyTeamsWidget title="My teams" />,
      )

      // Assert
      expect(reportedState()).toBe('failed')
    })

    it('reports empty when the home context says the caller is on no team', () => {
      // Arrange
      mockHome.context = homeContext()
      mockSummary.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: false,
      })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyTeamsWidget title="My teams" />,
      )

      // Assert
      expect(reportedState()).toBe('empty')
    })

    it('reports empty when the summary lists no teams', () => {
      // Arrange
      mockSummary.mockReturnValue(loaded({}))

      // Act
      const { reportedState } = renderReportingWidget(
        <MyTeamsWidget title="My teams" />,
      )

      // Assert
      expect(reportedState()).toBe('empty')
    })

    it('reports rows when the summary lists a team', () => {
      // Arrange
      mockSummary.mockReturnValue(loaded({ teams: [team('a')] }))

      // Act
      const { reportedState } = renderReportingWidget(
        <MyTeamsWidget title="My teams" />,
      )

      // Assert
      expect(reportedState()).toBe('rows')
    })
  })
})

describe('MyTeamsWidget alongside teams of teams', () => {
  beforeEach(() => {
    mockHome.context = homeContext({
      teams: { teamCount: 1, teamOfTeamsCount: 1 },
    })
  })

  it('shows only the teams, saying which team of teams each is part of', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teams: [
          team('pay', {
            partOf: {
              id: 'art',
              key: 3,
              name: 'Release Train',
              code: 'ART',
              type: 'Team of Teams',
            },
          }),
        ],
        teamsOfTeams: [
          {
            team: {
              id: 'art',
              key: 3,
              name: 'Release Train',
              code: 'ART',
              type: 'Team of Teams',
            },
            scrumTeamCount: 0,
            kanbanTeamCount: 0,
            rows: [],
          },
        ],
      }),
    )

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(screen.getByRole('link', { name: 'Team pay' })).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'Release Train' })).toHaveLength(
      1,
    )
    expect(screen.getByText(/Part of/)).toHaveTextContent(
      'Part of Release Train',
    )
  })

  it('says how it fills for a member of a team of teams only', () => {
    // Arrange
    mockHome.context = homeContext({ teams: { teamOfTeamsCount: 1 } })
    mockSummary.mockReturnValue(
      loaded({
        teamsOfTeams: [
          {
            team: {
              id: 'art',
              key: 3,
              name: 'Release Train',
              code: 'ART',
              type: 'Team of Teams',
            },
            scrumTeamCount: 0,
            kanbanTeamCount: 0,
            rows: [],
          },
        ],
      }),
    )

    // Act
    render(<MyTeamsWidget title="My teams" />)

    // Assert
    expect(
      screen.queryByRole('link', { name: 'Release Train' }),
    ).not.toBeInTheDocument()
    expect(
      screen.getByText(/You are not a member of a team/),
    ).toBeInTheDocument()
  })
})
