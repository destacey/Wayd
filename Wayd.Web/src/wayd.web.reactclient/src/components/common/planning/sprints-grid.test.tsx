import { render, screen } from '@testing-library/react'

// Mock WaydGrid2 with a light stand-in that exposes the props under test.
jest.mock('../wayd-grid2', () => ({
  WaydGrid2: jest.fn(
    ({ data, isLoading, height, emptyMessage, columns, onRefresh }) => (
      <div data-testid="wayd-grid">
        <div data-testid="row-count">{data?.length ?? 0}</div>
        <div data-testid="loading">{isLoading ? 'loading' : 'not-loading'}</div>
        <div data-testid="height">{height}</div>
        <div data-testid="empty-message">{emptyMessage}</div>
        <div data-testid="column-count">{columns?.length ?? 0}</div>
        {onRefresh && (
          <button type="button" onClick={onRefresh} data-testid="load-data">
            Refresh
          </button>
        )}
      </div>
    ),
  ),
  // Link renderers are exercised elsewhere; stub them so the grid stays simple.
  renderSprintLink: jest.fn(() => null),
  renderTeamLink: jest.fn(() => null),
}))

// Note: useTheme and dayjs are mocked globally in jest.setup.ts

import SprintsGrid from './sprints-grid'
import * as WaydGrid2Module from '../wayd-grid2'
import { SprintListDto } from '@/src/services/wayd-api'

describe('SprintsGrid', () => {
  const mockRefetch = jest.fn()

  const mockSprints: SprintListDto[] = [
    {
      id: '1',
      key: 101,
      name: 'Sprint 1',
      state: { id: 1, name: 'Active' },
      start: new Date('2025-01-01'),
      end: new Date('2025-01-15'),
      team: { id: '1', key: 1, name: 'Team Alpha', code: 'TA', type: 'Team' },
    },
    {
      id: '2',
      key: 102,
      name: 'Sprint 2',
      state: { id: 2, name: 'Planned' },
      start: new Date('2025-01-16'),
      end: new Date('2025-01-30'),
      team: { id: '2', key: 2, name: 'Team Beta', code: 'TB', type: 'Team' },
    },
  ]

  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders the grid', () => {
    // Arrange / Act
    render(
      <SprintsGrid
        sprints={mockSprints}
        isLoading={false}
        refetch={mockRefetch}
      />,
    )

    // Assert
    expect(screen.getByTestId('wayd-grid')).toBeInTheDocument()
  })

  it('passes sprint data to the grid', () => {
    // Arrange / Act
    render(
      <SprintsGrid
        sprints={mockSprints}
        isLoading={false}
        refetch={mockRefetch}
      />,
    )

    // Assert
    expect(screen.getByTestId('row-count')).toHaveTextContent('2')
  })

  it('passes the loading state through', () => {
    // Arrange / Act
    render(
      <SprintsGrid sprints={mockSprints} isLoading={true} refetch={mockRefetch} />,
    )

    // Assert
    expect(screen.getByTestId('loading')).toHaveTextContent('loading')
  })

  it('renders with an empty sprints array', () => {
    // Arrange / Act
    render(<SprintsGrid sprints={[]} isLoading={false} refetch={mockRefetch} />)

    // Assert
    expect(screen.getByTestId('row-count')).toHaveTextContent('0')
  })

  it('uses the default grid height when not specified', () => {
    // Arrange / Act
    render(
      <SprintsGrid
        sprints={mockSprints}
        isLoading={false}
        refetch={mockRefetch}
      />,
    )

    // Assert
    expect(screen.getByTestId('height')).toHaveTextContent('650')
  })

  it('uses a custom grid height when specified', () => {
    // Arrange / Act
    render(
      <SprintsGrid
        sprints={mockSprints}
        isLoading={false}
        refetch={mockRefetch}
        gridHeight={500}
      />,
    )

    // Assert
    expect(screen.getByTestId('height')).toHaveTextContent('500')
  })

  it('displays the empty message', () => {
    // Arrange / Act
    render(<SprintsGrid sprints={[]} isLoading={false} refetch={mockRefetch} />)

    // Assert
    expect(screen.getByTestId('empty-message')).toHaveTextContent(
      'No sprints found.',
    )
  })

  it('defines the expected columns', () => {
    // Arrange / Act
    render(
      <SprintsGrid
        sprints={mockSprints}
        isLoading={false}
        refetch={mockRefetch}
      />,
    )

    // Assert — key, name, team, state, start, end
    const call = (WaydGrid2Module.WaydGrid2 as unknown as jest.Mock).mock
      .calls[0][0]
    const ids = call.columns.map((c: { id: string }) => c.id)
    expect(ids).toEqual(
      expect.arrayContaining(['key', 'name', 'team', 'state', 'start', 'end']),
    )
  })

  it('calls refetch when the refresh action fires', () => {
    // Arrange
    render(
      <SprintsGrid
        sprints={mockSprints}
        isLoading={false}
        refetch={mockRefetch}
      />,
    )

    // Act
    screen.getByTestId('load-data').click()

    // Assert
    expect(mockRefetch).toHaveBeenCalledTimes(1)
  })

  it('hides the team column via meta.hide when hideTeam is set', () => {
    // Arrange / Act
    render(
      <SprintsGrid
        sprints={mockSprints}
        isLoading={false}
        refetch={mockRefetch}
        hideTeam={true}
      />,
    )

    // Assert
    const call = (WaydGrid2Module.WaydGrid2 as unknown as jest.Mock).mock
      .calls[0][0]
    const teamColumn = call.columns.find(
      (c: { id: string }) => c.id === 'team',
    )
    expect(teamColumn.meta.hide).toBe(true)
  })

  it('does not hide the team column by default', () => {
    // Arrange / Act
    render(
      <SprintsGrid
        sprints={mockSprints}
        isLoading={false}
        refetch={mockRefetch}
      />,
    )

    // Assert
    const call = (WaydGrid2Module.WaydGrid2 as unknown as jest.Mock).mock
      .calls[0][0]
    const teamColumn = call.columns.find(
      (c: { id: string }) => c.id === 'team',
    )
    expect(teamColumn.meta.hide).toBeUndefined()
  })
})
