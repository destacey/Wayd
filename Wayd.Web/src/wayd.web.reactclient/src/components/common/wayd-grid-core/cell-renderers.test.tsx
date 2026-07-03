import { render, screen } from '@testing-library/react'

import { WorkStatusCategory } from '@/src/components/types'

import {
  renderAssignedToLink,
  renderPlanningIntervalLink,
  renderPortfolioLink,
  renderProgramLink,
  renderProjectLink,
  renderSprintLink,
  renderTeamLink,
  renderUserLink,
  renderWorkItemLink,
  renderWorkStatusTag,
  renderWorkspaceLink,
} from './cell-renderers'

jest.mock('next/link', () => {
  const MockLink = ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  )
  MockLink.displayName = 'MockLink'
  return MockLink
})

describe('renderTeamLink', () => {
  it('renders a link to a Team using its name', () => {
    // Arrange / Act
    render(<>{renderTeamLink({ key: 12, name: 'Team Juice', type: 'Team' })}</>)

    // Assert
    const link = screen.getByRole('link', { name: 'Team Juice' })
    expect(link).toHaveAttribute('href', '/organizations/teams/12')
  })

  it('renders a team-of-teams link when type is not Team', () => {
    // Arrange / Act
    render(
      <>
        {renderTeamLink({ key: 3, name: 'Platform Group', type: 'TeamOfTeams' })}
      </>,
    )

    // Assert
    const link = screen.getByRole('link', { name: 'Platform Group' })
    expect(link).toHaveAttribute('href', '/organizations/team-of-teams/3')
  })

  it('renders nothing for a missing team', () => {
    // Arrange / Act
    const { container } = render(<>{renderTeamLink(null)}</>)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})

describe('NavigationDto link renderers', () => {
  const cases: Array<{
    name: string
    render: (
      e: { key: number; name: string } | null,
    ) => React.ReactNode
    href: string
  }> = [
    {
      name: 'planning interval',
      render: renderPlanningIntervalLink,
      href: '/planning/planning-intervals/8',
    },
    { name: 'project', render: renderProjectLink, href: '/ppm/projects/8' },
    {
      name: 'portfolio',
      render: renderPortfolioLink,
      href: '/ppm/portfolios/8',
    },
    { name: 'program', render: renderProgramLink, href: '/ppm/programs/8' },
    {
      name: 'workspace',
      render: renderWorkspaceLink,
      href: '/work/workspaces/8',
    },
  ]

  it.each(cases)('renders a $name link to the right route', ({ render: r, href }) => {
    // Arrange / Act
    render(<>{r({ key: 8, name: 'Q3' })}</>)

    // Assert
    expect(screen.getByRole('link', { name: 'Q3' })).toHaveAttribute(
      'href',
      href,
    )
  })

  it.each(cases)('renders nothing for a missing $name', ({ render: r }) => {
    // Arrange / Act
    const { container } = render(<>{r(null)}</>)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})

describe('renderSprintLink', () => {
  it('appends the team code by default when present', () => {
    // Arrange / Act
    render(
      <>
        {renderSprintLink({ key: 4, name: 'Sprint 12', team: { code: 'JCE' } })}
      </>,
    )

    // Assert
    const link = screen.getByRole('link', { name: 'Sprint 12 (JCE)' })
    expect(link).toHaveAttribute('href', '/planning/sprints/4')
  })

  it('omits the code when showTeamCode is false', () => {
    // Arrange / Act
    render(
      <>
        {renderSprintLink(
          { key: 4, name: 'Sprint 12', team: { code: 'JCE' } },
          { showTeamCode: false },
        )}
      </>,
    )

    // Assert
    expect(screen.getByRole('link', { name: 'Sprint 12' })).toBeInTheDocument()
  })

  it('shows just the name when there is no team code', () => {
    // Arrange / Act
    render(<>{renderSprintLink({ key: 4, name: 'Sprint 12' })}</>)

    // Assert
    expect(screen.getByRole('link', { name: 'Sprint 12' })).toBeInTheDocument()
  })

  it('renders nothing for a missing sprint', () => {
    // Arrange / Act
    const { container } = render(<>{renderSprintLink(null)}</>)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})

describe('renderUserLink', () => {
  it('links a user by id, labeled by userName', () => {
    // Arrange / Act
    render(<>{renderUserLink({ id: 'u-1', userName: 'ada' })}</>)

    // Assert
    const link = screen.getByRole('link', { name: 'ada' })
    expect(link).toHaveAttribute(
      'href',
      '/settings/user-management/users/u-1',
    )
  })

  it('renders nothing for a missing user', () => {
    // Arrange / Act
    const { container } = render(<>{renderUserLink(null)}</>)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})

describe('renderWorkItemLink', () => {
  it('links a work item within its workspace, labeled by key', () => {
    // Arrange / Act
    render(
      <>{renderWorkItemLink({ key: 'WEB-42', workspaceKey: 'WEB' })}</>,
    )

    // Assert
    const link = screen.getByRole('link', { name: 'WEB-42' })
    expect(link).toHaveAttribute(
      'href',
      '/work/workspaces/WEB/work-items/WEB-42',
    )
  })

  it('uses a custom label when provided (e.g. a parent title)', () => {
    // Arrange / Act
    render(
      <>
        {renderWorkItemLink({
          key: 'WEB-42',
          workspaceKey: 'WEB',
          label: 'Parent title',
        })}
      </>,
    )

    // Assert
    expect(
      screen.getByRole('link', { name: 'Parent title' }),
    ).toBeInTheDocument()
  })

  it('adds an external-system link when a URL is present', () => {
    // Arrange / Act
    render(
      <>
        {renderWorkItemLink({
          key: 'WEB-42',
          workspaceKey: 'WEB',
          externalViewWorkItemUrl: 'https://example.com/WEB-42',
        })}
      </>,
    )

    // Assert — the primary link plus the external one
    expect(
      screen.getByRole('link', { name: 'WEB-42' }),
    ).toBeInTheDocument()
    const external = screen.getByTitle('Open in external system')
    expect(external).toHaveAttribute('href', 'https://example.com/WEB-42')
    expect(external).toHaveAttribute('target', '_blank')
  })

  it('renders nothing for a missing work item', () => {
    // Arrange / Act
    const { container } = render(<>{renderWorkItemLink(null)}</>)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})

describe('renderAssignedToLink', () => {
  it('links an assignee to their organization page by key', () => {
    // Arrange / Act
    render(<>{renderAssignedToLink({ key: 13, name: 'Ada Lovelace' })}</>)

    // Assert
    const link = screen.getByRole('link', { name: 'Ada Lovelace' })
    expect(link).toHaveAttribute('href', '/organizations/employees/13')
  })

  it('renders nothing when unassigned', () => {
    // Arrange / Act
    const { container } = render(<>{renderAssignedToLink(null)}</>)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})

describe('renderWorkStatusTag', () => {
  it('renders the status text as a tag', () => {
    // Arrange / Act
    render(
      <>
        {renderWorkStatusTag({
          status: 'In Progress',
          statusCategory: { id: WorkStatusCategory.Active },
        })}
      </>,
    )

    // Assert
    expect(screen.getByText('In Progress')).toBeInTheDocument()
  })

  it('renders nothing when there is no status', () => {
    // Arrange / Act
    const { container } = render(
      <>
        {renderWorkStatusTag({
          status: '',
          statusCategory: { id: WorkStatusCategory.Proposed },
        })}
      </>,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
