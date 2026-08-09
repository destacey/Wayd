import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import ProjectCard from './project-card'
import { ProjectListDto } from '@/src/services/wayd-api'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

// Mock child components that make API calls
jest.mock('./project-stat-pills', () => {
  const MockStatPills = () => <div data-testid="stat-pills" />
  MockStatPills.displayName = 'MockStatPills'
  return MockStatPills
})

jest.mock('@/src/app/ppm/projects/_components/project-health-check-tag', () => {
  const MockHealthCheckTag = () => null
  MockHealthCheckTag.displayName = 'MockHealthCheckTag'
  return MockHealthCheckTag
})

function createProject(
  overrides?: Partial<ProjectListDto>,
): ProjectListDto {
  return {
    id: 'proj-1',
    key: 'PROJ1',
    name: 'Test Project',
    status: { name: 'Active', lifecycleCategory: 'Active' } as any,
    portfolio: { id: 'port-1', key: 1, name: 'Portfolio' } as any,
    projectSponsors: [],
    projectOwners: [],
    projectManagers: [],
    projectMembers: [],
    strategicThemes: [],
    phases: [],
    ...overrides,
  } as ProjectListDto
}

describe('ProjectCard', () => {
  const defaultProps = {
    isSelected: false,
    employeeId: 'emp-1',
    onSelect: jest.fn(),
  }

  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders project name and key', () => {
    render(
      <ProjectCard
        {...defaultProps}
        project={createProject()}
      />,
    )

    expect(screen.getByText('Test Project')).toBeInTheDocument()
    expect(screen.getByText('PROJ1')).toBeInTheDocument()
  })

  it('renders lifecycle status tag', () => {
    render(
      <ProjectCard
        {...defaultProps}
        project={createProject()}
      />,
    )

    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('renders user role when user is on the project', () => {
    const project = createProject({
      projectOwners: [{ id: 'emp-1', key: 1, name: 'Me' }] as any[],
    })

    render(<ProjectCard {...defaultProps} project={project} />)

    expect(screen.getByText('Owner')).toBeInTheDocument()
  })

  it('renders multiple roles joined with dot separator', () => {
    const project = createProject({
      projectSponsors: [{ id: 'emp-1', key: 1, name: 'Me' }] as any[],
      projectManagers: [{ id: 'emp-1', key: 1, name: 'Me' }] as any[],
    })

    render(<ProjectCard {...defaultProps} project={project} />)

    expect(screen.getByText('Sponsor · PM')).toBeInTheDocument()
  })

  it('does not render role badge when user has no core team role', () => {
    render(
      <ProjectCard
        {...defaultProps}
        project={createProject()}
      />,
    )

    expect(screen.queryByText(/Sponsor|Owner|PM|Member/)).not.toBeInTheDocument()
  })

  it('renders end date when set', () => {
    const project = createProject({
      end: new Date('2026-05-10T00:00:00'),
    })

    render(<ProjectCard {...defaultProps} project={project} />)

    expect(screen.getByText('End May 10, 2026')).toBeInTheDocument()
  })

  it('does not render end date when not set', () => {
    render(
      <ProjectCard
        {...defaultProps}
        project={createProject()}
      />,
    )

    expect(screen.queryByText(/End /)).not.toBeInTheDocument()
  })

  it('calls onSelect with project key when clicked', async () => {
    const onSelect = jest.fn()

    render(
      <ProjectCard
        {...defaultProps}
        onSelect={onSelect}
        project={createProject()}
      />,
    )

    await userEvent.click(screen.getByText('Test Project'))

    expect(onSelect).toHaveBeenCalledWith('PROJ1')
  })

  it('renders stat pills component', () => {
    render(
      <ProjectCard
        {...defaultProps}
        project={createProject()}
      />,
    )

    expect(screen.getByTestId('stat-pills')).toBeInTheDocument()
  })
})
