import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import TeamOverview from './team-overview'
import { TeamDetailsDto } from '@/src/services/wayd-api'
import {
  useGetTeamBacklogQuery,
  useGetTeamDependenciesQuery,
  useGetTeamRisksQuery,
} from '@/src/store/features/organizations/team-api'
import { useGetTeamMembersQuery } from '@/src/store/features/organization/team-members-api'
import { useGetTeamCapacityQuery } from '@/src/store/features/organization/team-capacity-api'

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamBacklogQuery: jest.fn(),
  useGetTeamDependenciesQuery: jest.fn(),
  useGetTeamRisksQuery: jest.fn(),
}))

jest.mock('@/src/store/features/organization/team-members-api', () => ({
  useGetTeamMembersQuery: jest.fn(),
}))

jest.mock('@/src/store/features/organization/team-capacity-api', () => ({
  useGetTeamCapacityQuery: jest.fn(),
}))

const mockBacklog = useGetTeamBacklogQuery as unknown as jest.Mock
const mockDependencies = useGetTeamDependenciesQuery as unknown as jest.Mock
const mockRisks = useGetTeamRisksQuery as unknown as jest.Mock
const mockMembers = useGetTeamMembersQuery as unknown as jest.Mock
const mockCapacity = useGetTeamCapacityQuery as unknown as jest.Mock

// No operating model, so the sprint and cadence summaries stay out of the way.
const team = { id: 'team-1', key: 7, name: 'Platform Core' } as TeamDetailsDto

const navigate = jest.fn()
const renderOverview = (capacityEnabled = true, forTeam = team) =>
  render(
    <TeamOverview
      team={forTeam}
      onNavigateToSection={navigate}
      capacityEnabled={capacityEnabled}
    />,
  )

const person = (...percents: number[]) => ({
  allocations: percents.map((percent) => ({ percent })),
})

describe('TeamOverview', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockBacklog.mockReturnValue({ data: [], isLoading: false })
    mockDependencies.mockReturnValue({ data: [], isLoading: false })
    mockRisks.mockReturnValue({ data: [], isLoading: false })
    mockMembers.mockReturnValue({ data: [], isLoading: false })
    mockCapacity.mockReturnValue({ currentData: undefined, isFetching: false })
  })

  afterEach(() => {
    jest.useRealTimers()
  })

  it("shows today's allocations as full-time equivalents", () => {
    // Arrange
    mockCapacity.mockReturnValue({
      currentData: { people: [person(100), person(50, 30), person(40)] },
      isFetching: false,
    })

    // Act
    renderOverview()

    // Assert
    const tile = screen.getByRole('button', { name: 'Capacity' })
    expect(tile).toHaveTextContent('2.2')
    expect(tile).toHaveTextContent('FTE')
  })

  it("asks for capacity on today's date in the team's time zone", () => {
    // Arrange
    // Noon UTC on 10 March is already 11 March in Auckland (UTC+13).
    jest.useFakeTimers().setSystemTime(new Date('2026-03-10T12:00:00Z'))
    const aucklandTeam = {
      ...team,
      operatingModel: { timeZone: 'Pacific/Auckland' },
    } as TeamDetailsDto

    // Act
    renderOverview(true, aucklandTeam)

    // Assert
    const [args, options] = mockCapacity.mock.calls[0]
    expect(args).toEqual({
      teamId: team.id,
      teamType: 'Team',
      from: '2026-03-11',
      to: '2026-03-11',
    })
    expect(options.skip).toBe(false)
  })

  it('shows a skeleton while the capacity loads', () => {
    // Arrange
    mockCapacity.mockReturnValue({ currentData: undefined, isFetching: true })

    // Act
    renderOverview()

    // Assert
    const tile = screen.getByRole('button', { name: 'Capacity' })
    expect(tile).not.toHaveTextContent('FTE')
  })

  it('opens the capacity section when the tile is clicked', async () => {
    // Arrange
    mockCapacity.mockReturnValue({
      currentData: { people: [person(100)] },
      isFetching: false,
    })
    renderOverview()

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Capacity' }))

    // Assert
    expect(navigate).toHaveBeenCalledWith('capacity')
  })

  it.each([
    ['nobody is allocated today', []],
    ['the allocations round to 0.0 FTE', [person(4)]],
  ])('omits the tile when %s', (_case, people) => {
    // Arrange
    mockCapacity.mockReturnValue({
      currentData: { people },
      isFetching: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(screen.getByRole('button', { name: 'Members' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Capacity' })).toBeNull()
  })

  it('shows the tile once the allocations reach 0.1 FTE', () => {
    // Arrange
    mockCapacity.mockReturnValue({
      currentData: { people: [person(5)] },
      isFetching: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(screen.getByRole('button', { name: 'Capacity' })).toHaveTextContent(
      '0.1',
    )
  })

  it('omits the tile and skips the query when capacity management is off', () => {
    // Arrange
    mockCapacity.mockReturnValue({
      currentData: { people: [person(100)] },
      isFetching: false,
    })

    // Act
    renderOverview(false)

    // Assert
    expect(screen.queryByRole('button', { name: 'Capacity' })).toBeNull()
    expect(mockCapacity.mock.calls[0][1].skip).toBe(true)
  })
})
