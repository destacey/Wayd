import React from 'react'
import { render, screen } from '@testing-library/react'
import EmployeeTeamsSummary from './employee-teams-summary'
import { useGetEmployeeTeamMembershipsQuery } from '@/src/store/features/organization/team-members-api'

jest.mock('@/src/store/features/organization/team-members-api', () => ({
  useGetEmployeeTeamMembershipsQuery: jest.fn(),
}))

const mockQuery = useGetEmployeeTeamMembershipsQuery as unknown as jest.Mock

const membership = (
  key: number,
  name: string,
  roles: { id: string; name: string }[] = [],
) => ({
  employee: { id: 'e1' },
  team: { id: `t${key}`, key, name },
  roles,
})

describe('EmployeeTeamsSummary', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders a link and roles for each membership', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [
        membership(14, 'Platform Core', [{ id: 'r1', name: 'Scrum Master' }]),
      ],
      isLoading: false,
    })

    // Act
    render(<EmployeeTeamsSummary employeeId="e1" />)

    // Assert
    expect(screen.getByRole('link', { name: 'Platform Core' })).toHaveAttribute(
      'href',
      '/organizations/teams/14',
    )
    expect(screen.getByText('Scrum Master')).toBeInTheDocument()
  })

  it('sorts teams case-insensitively', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [membership(2, 'identity guild'), membership(1, 'Platform Core')],
      isLoading: false,
    })

    // Act
    render(<EmployeeTeamsSummary employeeId="e1" />)

    // Assert
    const links = screen.getAllByRole('link')
    expect(links.map((l) => l.textContent)).toEqual([
      'identity guild',
      'Platform Core',
    ])
  })

  it('falls back to Member when a membership carries no roles', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [membership(1, 'Platform Core')],
      isLoading: false,
    })

    // Act
    render(<EmployeeTeamsSummary employeeId="e1" />)

    // Assert
    expect(screen.getByText('Member')).toBeInTheDocument()
  })

  it('shows an empty state when there are no memberships', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: [], isLoading: false })

    // Act
    render(<EmployeeTeamsSummary employeeId="e1" />)

    // Assert
    expect(
      screen.getByText('Not a member of any team.'),
    ).toBeInTheDocument()
  })

  it('skips the query until an employee id is known', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: undefined, isLoading: false })

    // Act
    render(<EmployeeTeamsSummary employeeId="" />)

    // Assert
    expect(mockQuery).toHaveBeenCalledWith(
      { employeeId: '' },
      { skip: true },
    )
  })
})
