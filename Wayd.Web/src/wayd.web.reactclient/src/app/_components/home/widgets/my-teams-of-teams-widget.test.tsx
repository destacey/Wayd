import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import MyTeamsOfTeamsWidget from './my-teams-of-teams-widget'
import {
  HomeContextResponse,
  MyTeamOfTeamsSummaryResponse,
  MyTeamsSummaryResponse,
} from '@/src/services/wayd-api'
import { homeContext } from '../home-context-test-utils'

const mockHome: { context?: HomeContextResponse } = {}

vi.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => true }),
}))

vi.mock('@/src/store/features/user-management/me-api', () => ({
  useGetMyTeamsSummaryQuery: vi.fn(),
}))

import { useGetMyTeamsSummaryQuery } from '@/src/store/features/user-management/me-api'

const mockSummary = useGetMyTeamsSummaryQuery as Mock

const rollup = (name: string): MyTeamOfTeamsSummaryResponse => ({
  team: {
    id: name.toLowerCase(),
    key: 3,
    name,
    code: name.slice(0, 3).toUpperCase(),
    type: 'Team of Teams',
  },
  scrumTeamCount: 0,
  kanbanTeamCount: 0,
  rows: [],
})

const loaded = (summary: Partial<MyTeamsSummaryResponse>) => ({
  data: { teams: [], teamsOfTeams: [], dependenciesAtRiskCount: 0, ...summary },
  isLoading: false,
  isError: false,
})

describe('MyTeamsOfTeamsWidget', () => {
  beforeEach(() => {
    mockHome.context = homeContext({
      teams: { teamCount: 1, teamOfTeamsCount: 1 },
    })
  })

  it('draws a card for each team of teams and none for the teams', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teams: [
          {
            team: {
              id: 'pay',
              key: 7,
              name: 'Team pay',
              code: 'PAY',
              type: 'Team',
            },
          },
        ],
        teamsOfTeams: [rollup('Release Train')],
      }),
    )

    // Act
    render(<MyTeamsOfTeamsWidget title="My teams of teams" />)

    // Assert
    expect(
      screen.getByRole('link', { name: 'Release Train' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Team pay' }),
    ).not.toBeInTheDocument()
  })

  it('links View all to the Teams list', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({ teamsOfTeams: [rollup('Release Train')] }),
    )

    // Act
    render(<MyTeamsOfTeamsWidget title="My teams of teams" />)

    // Assert
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/organizations/teams',
    )
  })

  it('says how it fills for a team member on no team of teams', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded({}))

    // Act
    render(<MyTeamsOfTeamsWidget title="My teams of teams" />)

    // Assert
    expect(
      screen.getByText(/You are not a member of a team of teams/),
    ).toBeInTheDocument()
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
    render(<MyTeamsOfTeamsWidget title="My teams of teams" />)

    // Assert
    expect(
      screen.getByText(/You are not a member of a team of teams/),
    ).toBeInTheDocument()
    expect(mockSummary).toHaveBeenCalledWith(undefined, { skip: true })
  })

  it('says when the teams of teams could not be loaded', () => {
    // Arrange
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<MyTeamsOfTeamsWidget title="My teams of teams" />)

    // Assert
    expect(
      screen.getByText('Could not load your teams of teams.'),
    ).toBeInTheDocument()
  })
})
