import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  CrossTeamDependenciesTile,
  DependenciesAtRiskTile,
  SprintsToCompleteTile,
} from './my-teams-tile'
import {
  HomeContextResponse,
  MyTeamsSummaryResponse,
} from '@/src/services/wayd-api'
import { homeContext } from '../home-context-test-utils'
import { widgetAnchorId } from '../widget-registry'

const mockHome: { context?: HomeContextResponse } = {}

vi.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/store/features/user-management/me-api', () => ({
  useGetMyTeamsSummaryQuery: vi.fn(),
}))

import { useGetMyTeamsSummaryQuery } from '@/src/store/features/user-management/me-api'

const mockSummary = useGetMyTeamsSummaryQuery as Mock

const loaded = (summary: Partial<MyTeamsSummaryResponse>) => ({
  data: { teams: [], teamsOfTeams: [], dependenciesAtRiskCount: 0, ...summary },
  isLoading: false,
  isError: false,
})

describe('DependenciesAtRiskTile', () => {
  beforeEach(() => {
    mockHome.context = homeContext({ teams: { teamCount: 2 } })
  })

  it('counts the dependencies at risk across the teams', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded({ dependenciesAtRiskCount: 3 }))

    // Act
    render(<DependenciesAtRiskTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Dependencies at risk on your teams: 3',
      }),
    ).toBeInTheDocument()
  })

  it('reads zero when the caller may not see dependencies', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded({ dependenciesAtRiskCount: undefined }))

    // Act
    render(<DependenciesAtRiskTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Dependencies at risk on your teams: 0',
      }),
    ).toBeInTheDocument()
  })

  it('reads zero, without asking, for someone on no team', () => {
    // Arrange
    mockHome.context = homeContext()
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<DependenciesAtRiskTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Dependencies at risk on your teams: 0',
      }),
    ).toBeInTheDocument()
    expect(mockSummary).toHaveBeenCalledWith(undefined, { skip: true })
  })

  it('shows a dash when the summary could not be loaded', () => {
    // Arrange
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<DependenciesAtRiskTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Dependencies at risk on your teams: unavailable',
      }),
    ).toBeInTheDocument()
  })
})

describe('team of teams tiles', () => {
  beforeEach(() => {
    mockHome.context = homeContext({ teams: { teamOfTeamsCount: 1 } })
  })

  it('asks for the summary for a member of a team of teams only', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded({ sprintsEndedNotCompletedCount: 2 }))

    // Act
    render(<SprintsToCompleteTile />)

    // Assert
    expect(mockSummary).toHaveBeenCalledWith(undefined, { skip: false })
    expect(
      screen.getByRole('button', {
        name: 'Sprints to complete in your teams of teams: 2',
      }),
    ).toBeInTheDocument()
  })

  it('counts the unhealthy cross-team dependencies', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({ unhealthyCrossTeamDependencyCount: 4 }),
    )

    // Act
    render(<CrossTeamDependenciesTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Unhealthy dependencies across your teams of teams: 4',
      }),
    ).toBeInTheDocument()
  })

  it('reads zero when the caller may not see the count', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({ unhealthyCrossTeamDependencyCount: undefined }),
    )

    // Act
    render(<CrossTeamDependenciesTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Unhealthy dependencies across your teams of teams: 0',
      }),
    ).toBeInTheDocument()
  })
})

describe('team tile targets', () => {
  beforeEach(() => {
    mockHome.context = homeContext({
      teams: { teamCount: 1, teamOfTeamsCount: 1 },
    })
    mockSummary.mockReturnValue(loaded({}))
  })

  it.each([
    ['DependenciesAtRiskTile', DependenciesAtRiskTile, 'my-teams'],
    ['SprintsToCompleteTile', SprintsToCompleteTile, 'my-teams-of-teams'],
    [
      'CrossTeamDependenciesTile',
      CrossTeamDependenciesTile,
      'my-teams-of-teams',
    ],
  ] as const)('%s scrolls to the %s widget', async (_, Tile, widgetId) => {
    // Arrange
    const target = document.createElement('section')
    target.id = widgetAnchorId(widgetId)
    target.scrollIntoView = vi.fn()
    document.body.appendChild(target)
    render(<Tile />)

    // Act
    await userEvent.click(screen.getByRole('button'))

    // Assert
    expect(target.scrollIntoView).toHaveBeenCalled()
    target.remove()
  })
})
