import { render, screen } from '@testing-library/react'
import TeamFacts from './team-facts'

const mockMemberships = jest.fn()

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOfTeamsMembershipsQuery: (...args: unknown[]) =>
    mockMemberships(...args),
}))

jest.mock('@/src/components/common/links/links-card', () => {
  const LinksCard = () => <div>Links Card</div>
  return LinksCard
})

const team = {
  id: 'team-1',
  key: 14,
  name: 'Platform Core',
  code: 'PLAT',
  type: 'Team',
  isActive: true,
  activeDate: new Date('2024-01-15'),
} as any

const childOf = (id: string, name: string) => ({
  parent: { id: 'team-1' },
  child: { id, key: 20, name, type: 'Team', code: 'C1' },
  end: undefined,
})

describe('TeamFacts', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockMemberships.mockReturnValue({ data: [], isLoading: false })
  })

  it('always shows the parent team, including when there is none', () => {
    // Arrange / Act
    render(<TeamFacts team={team} />)

    // Assert
    expect(screen.getByText('Parent Team')).toBeInTheDocument()
    expect(screen.getByText('None')).toBeInTheDocument()
  })

  it('links to the parent team when it has one', () => {
    // Arrange / Act
    render(
      <TeamFacts
        team={{ ...team, teamOfTeams: { id: 'tot-1', key: 3, name: 'Platform Tribe' } }}
      />,
    )

    // Assert
    expect(screen.getByRole('link', { name: 'Platform Tribe' })).toHaveAttribute(
      'href',
      '/organizations/team-of-teams/3',
    )
  })

  it('does not query for child teams on a plain team', () => {
    // Arrange / Act — a team has no children, so the request is pure waste.
    render(<TeamFacts team={team} />)

    // Assert
    expect(mockMemberships).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({ skip: true }),
    )
  })

  it('lists child teams for a team of teams', () => {
    // Arrange
    mockMemberships.mockReturnValue({
      data: [childOf('c2', 'Zeta Squad'), childOf('c1', 'Alpha Squad')],
      isLoading: false,
    })

    // Act
    render(<TeamFacts team={team} hasChildTeams />)

    // Assert — sorted, not in the order the API returned them
    const links = screen.getAllByRole('link')
    const names = links.map((l) => l.textContent)
    expect(names.indexOf('Alpha Squad')).toBeLessThan(names.indexOf('Zeta Squad'))
  })

  it('omits ended memberships, which are not live teams', () => {
    // Arrange
    mockMemberships.mockReturnValue({
      data: [{ ...childOf('c1', 'Disbanded Squad'), end: new Date('2025-01-01') }],
      isLoading: false,
    })

    // Act
    render(<TeamFacts team={team} hasChildTeams />)

    // Assert
    expect(screen.queryByText('Disbanded Squad')).toBeNull()
  })

  it('omits memberships where this record is the child, not the parent', () => {
    // Arrange — memberships run both ways, so a record's own parent arrives in
    // the same list and would otherwise be listed as one of its children.
    mockMemberships.mockReturnValue({
      data: [
        {
          parent: { id: 'someone-else' },
          child: { id: 'team-1', key: 14, name: 'Platform Core', type: 'Team' },
          end: undefined,
        },
      ],
      isLoading: false,
    })

    // Act
    render(<TeamFacts team={team} hasChildTeams />)

    // Assert
    expect(screen.queryByText('Teams')).toBeNull()
  })
})
