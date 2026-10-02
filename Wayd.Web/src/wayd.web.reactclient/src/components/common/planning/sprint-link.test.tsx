import React from 'react'
import { render, screen } from '@testing-library/react'
import SprintLink from './sprint-link'
import { WorkIterationNavigationDto } from '@/src/services/wayd-api'

// Mock Next.js Link component
jest.mock('next/link', () => {
  const MockedLink = ({ children, href }: { children: React.ReactNode; href: string }) => {
    return <a href={href}>{children}</a>
  }
  MockedLink.displayName = 'Link'
  return MockedLink
})

describe('SprintLink', () => {
  const mockSprintWithTeam: WorkIterationNavigationDto = {
    id: 'sprint-1',
    key: 101,
    name: 'Sprint 1',
    team: {
      id: 'team-1',
      key: 1,
      name: 'Team Alpha',
      code: 'TA',
      type: 'Team',
    },
  }

  const mockSprintWithoutTeamCode: WorkIterationNavigationDto = {
    id: 'sprint-2',
    key: 102,
    name: 'Sprint 2',
    team: {
      id: 'team-2',
      key: 2,
      name: 'Team Beta',
      code: undefined as unknown as string,
      type: 'Team',
    },
  }

  const mockSprintWithoutTeam: WorkIterationNavigationDto = {
    id: 'sprint-3',
    key: 103,
    name: 'Sprint 3',
  }

  it('renders sprint link with team code by default', () => {
    render(<SprintLink sprint={mockSprintWithTeam} />)

    const link = screen.getByRole('link')
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/work/sprints/101')
    expect(link).toHaveTextContent('Sprint 1 (TA)')
  })

  it('renders sprint link without team code when showTeamCode is false', () => {
    render(<SprintLink sprint={mockSprintWithTeam} showTeamCode={false} />)

    const link = screen.getByRole('link')
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/work/sprints/101')
    expect(link).toHaveTextContent('Sprint 1')
    expect(link).not.toHaveTextContent('(TA)')
  })

  it('renders sprint link without team code when team code is missing', () => {
    render(<SprintLink sprint={mockSprintWithoutTeamCode} />)

    const link = screen.getByRole('link')
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/work/sprints/102')
    expect(link).toHaveTextContent('Sprint 2')
    expect(link).not.toHaveTextContent('(')
  })

  it('renders sprint link without team code when team is missing', () => {
    render(<SprintLink sprint={mockSprintWithoutTeam} />)

    const link = screen.getByRole('link')
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/work/sprints/103')
    expect(link).toHaveTextContent('Sprint 3')
    expect(link).not.toHaveTextContent('(')
  })

  it('returns null when sprint is undefined', () => {
    const { container } = render(<SprintLink sprint={undefined} />)

    expect(container.firstChild).toBeNull()
  })

  it('returns null when sprint is null', () => {
    const { container } = render(<SprintLink sprint={null} />)

    expect(container.firstChild).toBeNull()
  })

  it('generates correct URL format', () => {
    const sprint: WorkIterationNavigationDto = {
      id: 'sprint-999',
      key: 999,
      name: 'Test Sprint',
    }

    render(<SprintLink sprint={sprint} />)

    const link = screen.getByRole('link')
    expect(link).toHaveAttribute('href', '/work/sprints/999')
  })

  it('handles sprint with team code and showTeamCode explicitly true', () => {
    render(<SprintLink sprint={mockSprintWithTeam} showTeamCode={true} />)

    const link = screen.getByRole('link')
    expect(link).toHaveTextContent('Sprint 1 (TA)')
  })
})
