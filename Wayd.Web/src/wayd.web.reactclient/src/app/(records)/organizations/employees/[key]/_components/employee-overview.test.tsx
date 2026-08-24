import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EmployeeOverview from './employee-overview'
import {
  useGetDirectReportsQuery,
  useGetEmployeeWorkItemsQuery,
} from '@/src/store/features/organizations/employee-api'
import { useGetEmployeeTeamMembershipsQuery } from '@/src/store/features/organization/team-members-api'
import { WorkStatusCategory } from '@/src/services/wayd-api'

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetDirectReportsQuery: jest.fn(),
  useGetEmployeeWorkItemsQuery: jest.fn(),
}))

jest.mock('@/src/store/features/organization/team-members-api', () => ({
  useGetEmployeeTeamMembershipsQuery: jest.fn(),
}))

const mockReports = useGetDirectReportsQuery as unknown as jest.Mock
const mockWorkItems = useGetEmployeeWorkItemsQuery as unknown as jest.Mock
const mockTeams = useGetEmployeeTeamMembershipsQuery as unknown as jest.Mock

const employee = { id: 'e1', displayName: 'Ada Lovelace' } as any

const navigate = jest.fn()
const renderOverview = () =>
  render(
    <EmployeeOverview employee={employee} onNavigateToSection={navigate} />,
  )

/** Assigned and completed work come from the same hook, keyed by status filter. */
const setWorkItems = ({
  open = [] as any[],
  completed = [] as any[],
} = {}) => {
  mockWorkItems.mockImplementation((args: any) =>
    args.statusCategories?.includes(WorkStatusCategory.Done)
      ? { data: completed, isLoading: false }
      : { data: open, isLoading: false },
  )
}

describe('EmployeeOverview', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockTeams.mockReturnValue({ data: [], isLoading: false })
    mockReports.mockReturnValue({ data: [], isLoading: false })
    setWorkItems()
  })

  it('omits the direct reports block and tile when there are none', () => {
    // Arrange / Act — most people manage nobody
    renderOverview()

    // Assert
    expect(screen.queryByText('Direct Reports')).toBeNull()
  })

  it('shows direct reports when the employee has them', () => {
    // Arrange
    mockReports.mockReturnValue({
      data: [{ id: 'r1', key: 7, displayName: 'Wei Chen', jobTitle: 'Engineer' }],
      isLoading: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(screen.getAllByText('Direct Reports').length).toBeGreaterThan(0)
    expect(screen.getByRole('link', { name: 'Wei Chen' })).toHaveAttribute(
      'href',
      '/organizations/employees/7',
    )
  })

  it('omits the teams block when the employee is on no teams', () => {
    // Arrange / Act — the tile still reports 0; a large empty card below it
    // would only take up space.
    renderOverview()

    // Assert — the tile keeps its title, so count how many 'Teams' appear:
    // one (the tile) means the block is absent.
    expect(screen.getAllByText('Teams')).toHaveLength(1)
  })

  it('shows the teams block once the employee is on a team', () => {
    // Arrange
    mockTeams.mockReturnValue({
      data: [
        {
          team: { id: 't1', key: 14, name: 'Platform Core' },
          roles: [],
        },
      ],
      isLoading: false,
    })

    // Act
    renderOverview()

    // Assert
    // Two now: the metric tile and the block heading below it.
    expect(screen.getAllByText('Teams')).toHaveLength(2)
  })

  it('omits the assigned work items tile when there is no open work', () => {
    // Arrange / Act
    renderOverview()

    // Assert
    expect(screen.queryByText('Assigned Work Items')).toBeNull()
  })

  it('shows the assigned work items tile when work is assigned', () => {
    // Arrange
    setWorkItems({ open: [{ key: 'PLT-1' }, { key: 'PLT-2' }] })

    // Act
    renderOverview()

    // Assert
    expect(screen.getByText('Assigned Work Items')).toBeInTheDocument()
    expect(screen.getByText('2')).toBeInTheDocument()
  })

  it('omits the cycle time tile when nothing completed in the window', () => {
    // Arrange / Act
    renderOverview()

    // Assert
    expect(screen.queryByText('Avg Cycle Time')).toBeNull()
  })

  it('averages cycle time over completed work, ignoring items without one', () => {
    // Arrange — 4 and 6 average to 5.0; the third item has no cycle time
    setWorkItems({
      completed: [
        { key: 'PLT-1', cycleTime: 4 },
        { key: 'PLT-2', cycleTime: 6 },
        { key: 'PLT-3' },
      ],
    })

    // Act
    renderOverview()

    // Assert — antd's Statistic splits the value across integer and decimal
    // spans, so match the rendered card rather than a single text node.
    // CycleTimeMetric renders at precision 2, as elsewhere in the app.
    expect(screen.getByText('Avg Cycle Time')).toBeInTheDocument()
    expect(screen.getByText('Last 90 days')).toBeInTheDocument()
    expect(
      screen.getByText('Avg Cycle Time').closest('.ant-card')?.textContent,
    ).toContain('5.00')
  })

  it('navigates to the section a tile summarises', async () => {
    // Arrange
    const user = userEvent.setup()
    setWorkItems({ open: [{ key: 'PLT-1' }] })
    renderOverview()

    // Act
    await user.click(screen.getByRole('button', { name: 'Assigned Work Items' }))

    // Assert
    expect(navigate).toHaveBeenCalledWith('work-items')
  })

  it('reaches a tile by keyboard, not only by mouse', async () => {
    // Arrange
    const user = userEvent.setup()
    renderOverview()

    // Act
    screen.getByRole('button', { name: 'Teams' }).focus()
    await user.keyboard('{Enter}')

    // Assert
    expect(navigate).toHaveBeenCalledWith('teams')
  })

  it('leaves direct reports unlinked — it has no section of its own', () => {
    // Arrange
    mockReports.mockReturnValue({
      data: [{ id: 'r1', key: 7, displayName: 'Wei Chen' }],
      isLoading: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(screen.queryByRole('button', { name: 'Direct Reports' })).toBeNull()
  })

  it('requests completed work from the last 90 days', () => {
    // Arrange / Act
    renderOverview()

    // Assert — matches the cycle time report's own default window
    const doneCall = mockWorkItems.mock.calls.find((c) =>
      c[0].statusCategories?.includes(WorkStatusCategory.Done),
    )
    const doneFrom = new Date(doneCall![0].doneFrom)
    const daysAgo = Math.round(
      (Date.now() - doneFrom.getTime()) / (1000 * 60 * 60 * 24),
    )
    expect(daysAgo).toBeGreaterThanOrEqual(89)
    expect(daysAgo).toBeLessThanOrEqual(91)
  })
})
