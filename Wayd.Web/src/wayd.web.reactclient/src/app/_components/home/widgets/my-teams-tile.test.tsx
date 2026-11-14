import type { Mock } from 'vitest'
import { render, renderHook, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  CrossTeamDependenciesTile,
  DependenciesAtRiskTile,
  SprintsToCompleteTile,
  teamsOfTeamsPickSetting,
  teamsPickSetting,
  useMyTeamsSummary,
} from './my-teams-tile'
import { ReactNode } from 'react'
import { HomeSettingsContext } from '../home-settings-context'
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
    render(<DependenciesAtRiskTile title="Dependencies at risk" />)

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
    render(<DependenciesAtRiskTile title="Dependencies at risk" />)

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
    render(<DependenciesAtRiskTile title="Dependencies at risk" />)

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
    render(<DependenciesAtRiskTile title="Dependencies at risk" />)

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
    render(<SprintsToCompleteTile title="Sprints to complete" />)

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
    render(<CrossTeamDependenciesTile title="Unhealthy dependencies" />)

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
    render(<CrossTeamDependenciesTile title="Unhealthy dependencies" />)

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
    render(<Tile title="Tile" />)

    // Act
    await userEvent.click(screen.getByRole('button'))

    // Assert
    expect(target.scrollIntoView).toHaveBeenCalled()
    target.remove()
  })
})

describe('useMyTeamsSummary', () => {
  beforeEach(() => {
    mockSummary.mockClear()
    mockSummary.mockReturnValue(loaded({}))
    mockHome.context = homeContext({
      teams: { teamCount: 2, teamOfTeamsCount: 1 },
    })
  })

  const withSettings = (settings: Record<string, Record<string, string>>) =>
    function WithSettings({ children }: { children: ReactNode }) {
      return (
        <HomeSettingsContext value={settings}>{children}</HomeSettingsContext>
      )
    }

  it('asks for every team while neither widget is narrowed, so one joined later shows', () => {
    // Act
    renderHook(() => useMyTeamsSummary(), { wrapper: withSettings({}) })

    // Assert
    expect(mockSummary).toHaveBeenCalledWith(undefined, { skip: false })
  })

  const TEAM_A = '0190a1b2-0000-7000-8000-00000000000a'
  const TEAM_B = '0190a1b2-0000-7000-8000-00000000000b'
  const TRAIN = '0190a1b2-0000-7000-8000-0000000000c1'
  const navigation = (id: string, name: string) => ({
    id,
    key: 1,
    name,
    code: name.toUpperCase(),
    type: 'Team',
  })

  it('asks with both widgets’ choices, so the widgets and tiles share one request', () => {
    // Act
    renderHook(() => useMyTeamsSummary(), {
      wrapper: withSettings({
        'my-teams': { teams: `${TEAM_A},${TEAM_B}` },
        'my-teams-of-teams': { teamsOfTeams: TRAIN },
      }),
    })

    // Assert
    expect(mockSummary).toHaveBeenCalledWith(
      { teamIds: [TEAM_A, TEAM_B], teamOfTeamsIds: [TRAIN] },
      { skip: false },
    )
  })

  it('leaves out a stored value that is not an id, which the server would refuse', () => {
    // Act
    renderHook(() => useMyTeamsSummary(), {
      wrapper: withSettings({ 'my-teams': { teams: `${TEAM_A},not-an-id` } }),
    })

    // Assert
    expect(mockSummary).toHaveBeenCalledWith(
      { teamIds: [TEAM_A], teamOfTeamsIds: [] },
      { skip: false },
    )
  })

  it('shows nothing, as loading, while a changed choice loads, but keeps the memberships', () => {
    // Arrange — RTK Query keeps the last summary in data while new arguments load
    const last = loaded({ allTeams: [navigation(TEAM_A, 'Alpha')] }).data
    mockSummary.mockReturnValue({
      data: last,
      currentData: undefined,
      isLoading: false,
      isFetching: true,
      isError: false,
    })

    // Act
    const { result } = renderHook(() => useMyTeamsSummary(), {
      wrapper: withSettings({ 'my-teams': { teams: TEAM_A } }),
    })

    // Assert
    expect(result.current.data).toBeUndefined()
    expect(result.current.isLoading).toBe(true)
    expect(result.current.memberships).toBe(last)
  })

  it('offers every team the caller is on, whatever the widget shows', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        allTeams: [navigation(TEAM_A, 'Alpha'), navigation(TEAM_B, 'Beta')],
      }),
    )
    const Wrapper = withSettings({ 'my-teams': { teams: TEAM_A } })
    const { Options } = teamsPickSetting
    const read = vi.fn(() => null)

    // Act
    render(
      <Wrapper>
        <Options>{read}</Options>
      </Wrapper>,
    )

    // Assert
    expect(read).toHaveBeenLastCalledWith({
      isLoading: false,
      isError: false,
      options: [
        { value: TEAM_A, label: 'Alpha' },
        { value: TEAM_B, label: 'Beta' },
      ],
    })
  })

  it('offers every team of teams the caller is on to My teams of teams', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({ allTeamsOfTeams: [navigation(TRAIN, 'Train')] }),
    )
    const { Options } = teamsOfTeamsPickSetting
    const read = vi.fn(() => null)

    // Act
    render(<Options>{read}</Options>)

    // Assert
    expect(read).toHaveBeenLastCalledWith({
      isLoading: false,
      isError: false,
      options: [{ value: TRAIN, label: 'Train' }],
    })
  })

  it('says the team list could not load rather than offering nothing', () => {
    // Arrange
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })
    const { Options } = teamsPickSetting
    const read = vi.fn(() => null)

    // Act
    render(<Options>{read}</Options>)

    // Assert
    expect(read).toHaveBeenLastCalledWith(
      expect.objectContaining({ isError: true, options: [] }),
    )
  })
})

describe('team tiles when narrowed', () => {
  beforeEach(() => {
    mockHome.context = homeContext({
      teams: { teamCount: 2, teamOfTeamsCount: 2 },
    })
  })

  const team = (id: string) => ({
    team: { id, key: 1, name: id, code: id, type: 'Team' },
  })

  it('says it counts the teams shown when My teams shows only some', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teams: [team('a') as never],
        allTeams: [team('a').team, team('b').team],
        dependenciesAtRiskCount: 1,
      }),
    )

    // Act
    render(<DependenciesAtRiskTile title="Dependencies at risk" />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Dependencies at risk on the teams shown: 1',
      }),
    ).toBeInTheDocument()
  })

  it('says it counts the teams of teams shown when My teams of teams shows only some', () => {
    // Arrange
    mockSummary.mockReturnValue(
      loaded({
        teamsOfTeams: [team('t') as never],
        allTeamsOfTeams: [team('t').team, team('u').team],
        sprintsEndedNotCompletedCount: 2,
        unhealthyCrossTeamDependencyCount: 3,
      }),
    )

    // Act
    render(
      <>
        <SprintsToCompleteTile title="Sprints to complete" />
        <CrossTeamDependenciesTile title="Unhealthy dependencies" />
      </>,
    )

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Sprints to complete in the teams of teams shown: 2',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', {
        name: 'Unhealthy dependencies across the teams of teams shown: 3',
      }),
    ).toBeInTheDocument()
  })
})
