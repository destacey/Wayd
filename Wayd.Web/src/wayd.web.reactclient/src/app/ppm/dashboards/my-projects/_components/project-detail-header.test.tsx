import { render, screen } from '@testing-library/react'
import ProjectDetailHeader from './project-detail-header'
import { ProjectDetailsDto } from '@/src/services/wayd-api'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

function createProject(
  overrides?: Partial<ProjectDetailsDto>,
): ProjectDetailsDto {
  return {
    id: 'proj-1',
    key: 'PROJ1',
    name: 'Test Project',
    description: '',
    status: {
      name: 'Active',
      lifecycleCategory: 'Active',
    } as any,
    expenditureCategory: { id: 1, name: 'Capital' } as any,
    portfolio: { id: 'port-1', key: 1, name: 'Portfolio' } as any,
    projectSponsors: [],
    projectOwners: [],
    projectManagers: [],
    projectMembers: [],
    strategicThemes: [],
    stages: [],
    ...overrides,
  } as ProjectDetailsDto
}

describe('ProjectDetailHeader', () => {
  it('renders project name', () => {
    render(<ProjectDetailHeader project={createProject()} />)

    expect(screen.getByText('Test Project')).toBeInTheDocument()
  })

  it('renders lifecycle status tag', () => {
    render(<ProjectDetailHeader project={createProject()} />)

    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('renders the project name as a link to the project details page', () => {
    render(<ProjectDetailHeader project={createProject()} />)

    const link = screen.getByRole('link', { name: 'Test Project' })
    expect(link).toHaveAttribute('href', '/ppm/projects/PROJ1')
  })

  it('renders owner names', () => {
    const project = createProject({
      projectOwners: [
        { id: '1', key: 1, name: 'Alice' },
        { id: '2', key: 2, name: 'Bob' },
      ] as any[],
    })

    render(<ProjectDetailHeader project={project} />)

    expect(screen.getByText('Owner: Alice, Bob')).toBeInTheDocument()
  })

  it('renders PM names', () => {
    const project = createProject({
      projectManagers: [{ id: '1', key: 1, name: 'Charlie' }] as any[],
    })

    render(<ProjectDetailHeader project={project} />)

    expect(screen.getByText('PM: Charlie')).toBeInTheDocument()
  })

  it('renders lifecycle name when assigned', () => {
    const project = createProject({
      projectLifecycle: { id: 'lc-1', key: 1, name: 'Standard' } as any,
    })

    render(<ProjectDetailHeader project={project} />)

    expect(screen.getByText('Lifecycle: Standard')).toBeInTheDocument()
  })

  it('renders fallback text when no lifecycle assigned', () => {
    const project = createProject({ projectLifecycle: undefined })

    render(<ProjectDetailHeader project={project} />)

    expect(
      screen.getByText('Lifecycle: No lifecycle assigned'),
    ).toBeInTheDocument()
  })

  it('renders program name as a link when assigned', () => {
    const project = createProject({
      program: { id: 'prog-1', key: 42, name: 'Alpha Program' } as any,
    })

    render(<ProjectDetailHeader project={project} />)

    expect(screen.getByText('Alpha Program')).toBeInTheDocument()
    const link = screen.getByRole('link', { name: 'Alpha Program' })
    expect(link).toHaveAttribute('href', '/ppm/programs/42')
  })

  it('does not render program when not assigned', () => {
    render(<ProjectDetailHeader project={createProject()} />)

    expect(screen.queryByText(/Program:/)).not.toBeInTheDocument()
  })

  it('does not render owner when none assigned', () => {
    render(<ProjectDetailHeader project={createProject()} />)

    expect(screen.queryByText(/Owner:/)).not.toBeInTheDocument()
  })

  it('does not render PM when none assigned', () => {
    render(<ProjectDetailHeader project={createProject()} />)

    expect(screen.queryByText(/PM:/)).not.toBeInTheDocument()
  })
})
