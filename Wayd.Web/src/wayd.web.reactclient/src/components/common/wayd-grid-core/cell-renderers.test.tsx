import { render, screen } from '@testing-library/react'

import {
  renderPlanningIntervalLink,
  renderPortfolioLink,
  renderProgramLink,
  renderProjectLink,
  renderSprintLink,
  renderTeamLink,
  renderUserLink,
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
