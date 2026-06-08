import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import PortfolioGroupSection from './portfolio-group-section'
import { PortfolioGroup } from './project-card-helpers'
import { ProjectListDto } from '@/src/services/wayd-api'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

// Mock ProjectCard to avoid deep rendering
jest.mock('./project-card', () => {
  const MockProjectCard = ({ project, isSelected, onSelect }: any) => (
    <div
      data-testid={`project-card-${project.key}`}
      data-selected={isSelected}
      onClick={() => onSelect(project.key)}
    >
      {project.name}
    </div>
  )
  MockProjectCard.displayName = 'MockProjectCard'
  return MockProjectCard
})

function createProject(
  key: string,
  name: string,
): ProjectListDto {
  return {
    id: `id-${key}`,
    key,
    name,
    status: { name: 'Active', lifecyclePhase: 'Active' } as any,
    portfolio: { id: 'port-1', key: 1, name: 'Portfolio' } as any,
    projectSponsors: [],
    projectOwners: [],
    projectManagers: [],
    projectMembers: [],
    strategicThemes: [],
    phases: [],
    rank: 1000,
    canManageProject: false,
  } as ProjectListDto
}

const defaultGroup: PortfolioGroup = {
  portfolioId: 'port-1',
  portfolioName: 'Product Delivery',
  projects: [
    createProject('P1', 'Project Alpha'),
    createProject('P2', 'Project Beta'),
  ],
}

describe('PortfolioGroupSection', () => {
  const defaultProps = {
    group: defaultGroup,
    selectedProjectKey: null as string | null,
    employeeId: 'emp-1',
    onSelectProject: jest.fn(),
  }

  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders portfolio name', () => {
    render(<PortfolioGroupSection {...defaultProps} />)

    expect(screen.getByText('Product Delivery')).toBeInTheDocument()
  })

  it('renders project count (plural)', () => {
    render(<PortfolioGroupSection {...defaultProps} />)

    expect(screen.getByText(/2\s+projects/)).toBeInTheDocument()
  })

  it('renders project count (singular)', () => {
    const group = {
      ...defaultGroup,
      projects: [createProject('P1', 'Solo Project')],
    }

    render(<PortfolioGroupSection {...defaultProps} group={group} />)

    expect(screen.getByText(/1\s+project$/)).toBeInTheDocument()
  })

  it('renders project cards when expanded (default)', () => {
    render(<PortfolioGroupSection {...defaultProps} />)

    expect(screen.getByText('Project Alpha')).toBeInTheDocument()
    expect(screen.getByText('Project Beta')).toBeInTheDocument()
  })

  it('hides project cards when collapsed', async () => {
    render(<PortfolioGroupSection {...defaultProps} />)

    // Click header to collapse
    await userEvent.click(screen.getByText('Product Delivery'))

    expect(screen.queryByText('Project Alpha')).not.toBeInTheDocument()
    expect(screen.queryByText('Project Beta')).not.toBeInTheDocument()
  })

  it('re-expands when header is clicked again', async () => {
    render(<PortfolioGroupSection {...defaultProps} />)

    await userEvent.click(screen.getByText('Product Delivery'))
    expect(screen.queryByText('Project Alpha')).not.toBeInTheDocument()

    await userEvent.click(screen.getByText('Product Delivery'))
    expect(screen.getByText('Project Alpha')).toBeInTheDocument()
  })

  it('passes selected state to project cards', () => {
    render(
      <PortfolioGroupSection
        {...defaultProps}
        selectedProjectKey="P1"
      />,
    )

    expect(screen.getByTestId('project-card-P1')).toHaveAttribute(
      'data-selected',
      'true',
    )
    expect(screen.getByTestId('project-card-P2')).toHaveAttribute(
      'data-selected',
      'false',
    )
  })

  it('calls onSelectProject when a project card is clicked', async () => {
    const onSelectProject = jest.fn()

    render(
      <PortfolioGroupSection
        {...defaultProps}
        onSelectProject={onSelectProject}
      />,
    )

    await userEvent.click(screen.getByText('Project Alpha'))

    expect(onSelectProject).toHaveBeenCalledWith('P1')
  })
})
