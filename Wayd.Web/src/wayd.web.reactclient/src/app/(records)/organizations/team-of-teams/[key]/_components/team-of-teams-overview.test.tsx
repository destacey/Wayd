import React from 'react'
import { render, screen } from '@testing-library/react'
import TeamOfTeamsOverview from './team-of-teams-overview'
import {
  useGetTeamOfTeamsMembershipsQuery,
  useGetTeamOfTeamsRisksQuery,
  useGetTeamOperatingModelsForTeamsQuery,
} from '@/src/store/features/organizations/team-api'
import { useGetTeamOfTeamsMembersQuery } from '@/src/store/features/organization/team-members-api'

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOfTeamsMembershipsQuery: jest.fn(),
  useGetTeamOfTeamsRisksQuery: jest.fn(),
  useGetTeamOperatingModelsForTeamsQuery: jest.fn(),
}))

jest.mock('@/src/store/features/organization/team-members-api', () => ({
  useGetTeamOfTeamsMembersQuery: jest.fn(),
}))

const mockMemberships = useGetTeamOfTeamsMembershipsQuery as unknown as jest.Mock
const mockRisks = useGetTeamOfTeamsRisksQuery as unknown as jest.Mock
const mockMembers = useGetTeamOfTeamsMembersQuery as unknown as jest.Mock
const mockOperatingModels =
  useGetTeamOperatingModelsForTeamsQuery as unknown as jest.Mock

const team = { id: 'tot-1', key: 3, name: 'Platform Tribe' } as any

const navigate = jest.fn()
const renderOverview = () =>
  render(
    <TeamOfTeamsOverview team={team} onNavigateToSection={navigate} />,
  )

const membership = (
  overrides: Partial<{
    id: string
    parentId: string
    child: any
    end?: Date
  }>,
) => ({
  id: overrides.id ?? 'm1',
  parent: { id: overrides.parentId ?? team.id },
  child: overrides.child,
  end: overrides.end,
})

describe('TeamOfTeamsOverview', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockMembers.mockReturnValue({ data: [], isLoading: false })
    mockRisks.mockReturnValue({ data: [], isLoading: false })
    mockMemberships.mockReturnValue({ data: [], isLoading: false })
    mockOperatingModels.mockReturnValue({ data: [], isLoading: false })
  })

  it('lists the child teams with their code and type', () => {
    // Arrange
    mockMemberships.mockReturnValue({
      data: [
        membership({
          child: { id: 'c1', key: 14, name: 'Platform Core', type: 'Team', code: 'PLAT-CORE' },
        }),
      ],
      isLoading: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(screen.getByRole('link', { name: 'Platform Core' })).toHaveAttribute(
      'href',
      '/organizations/teams/14',
    )
    expect(screen.getByText('PLAT-CORE')).toBeInTheDocument()
    // 'Team' also appears as the column header, so scope to the row.
    expect(screen.getAllByText('Team').length).toBeGreaterThan(1)
  })

  it('shows each team methodology from the batched operating models query', () => {
    // Arrange — one query covers every child team.
    mockMemberships.mockReturnValue({
      data: [
        membership({
          id: 'm1',
          child: { id: 'c1', key: 14, name: 'Platform Core', type: 'Team', code: 'PLAT' },
        }),
        membership({
          id: 'm2',
          child: { id: 'c2', key: 15, name: 'Infrastructure', type: 'Team', code: 'INFRA' },
        }),
      ],
      isLoading: false,
    })
    mockOperatingModels.mockReturnValue({
      data: [
        // isCurrent is false whenever a model has an end date — including one
        // still in effect. The query already filters to what is effective
        // today, so these must not be dropped.
        { teamId: 'c1', methodology: 'Scrum', isCurrent: false },
        { teamId: 'c2', methodology: 'Kanban', isCurrent: true },
      ],
      isLoading: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(screen.getByText('Scrum')).toBeInTheDocument()
    expect(screen.getByText('Kanban')).toBeInTheDocument()
  })

  it('shows each member job title beside their name', () => {
    // Arrange
    mockMembers.mockReturnValue({
      data: [
        {
          employee: {
            id: 'e1',
            key: 42,
            name: 'Ada Lovelace',
            jobTitle: 'Principal Engineer',
          },
          roles: [],
        },
      ],
      isLoading: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(screen.getByText('Principal Engineer')).toBeInTheDocument()
  })

  it('excludes memberships where this record is the child, not the parent', () => {
    // Arrange — a team-of-teams has its own parent, which arrives in the same
    // list and would otherwise be listed as if it were a child.
    mockMemberships.mockReturnValue({
      data: [
        membership({
          id: 'own-parent',
          parentId: 'someone-else',
          child: { id: team.id, key: 3, name: 'Platform Tribe', type: 'Team of Teams', code: 'PT' },
        }),
      ],
      isLoading: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(screen.getByText('No teams assigned.')).toBeInTheDocument()
  })

  it('excludes memberships that have ended', () => {
    // Arrange
    mockMemberships.mockReturnValue({
      data: [
        membership({
          child: { id: 'c1', key: 14, name: 'Former Team', type: 'Team', code: 'OLD' },
          end: new Date('2026-01-01'),
        }),
      ],
      isLoading: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(screen.getByText('No teams assigned.')).toBeInTheDocument()
  })

  it('routes a nested team-of-teams to its own route', () => {
    // Arrange — teamUrl branches on type; only 'Team' is a plain team
    mockMemberships.mockReturnValue({
      data: [
        membership({
          child: { id: 'c2', key: 9, name: 'Identity Program', type: 'Team of Teams', code: 'IDP' },
        }),
      ],
      isLoading: false,
    })

    // Act
    renderOverview()

    // Assert
    expect(
      screen.getByRole('link', { name: 'Identity Program' }),
    ).toHaveAttribute('href', '/organizations/team-of-teams/9')
  })
})
