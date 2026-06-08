import { act, render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import PortfolioGroupList from './portfolio-group-list'
import { ProjectListDto } from '@/src/services/wayd-api'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectsPlanSummariesQuery: jest.fn(() => ({ data: undefined })),
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    user: { employeeId: 'emp-1' },
    hasClaim: () => false,
    hasPermissionClaim: () => false,
  }),
}))

// Mock PortfolioGroupSection to avoid deep rendering
jest.mock('./portfolio-group-section', () => {
  const MockPortfolioGroupSection = ({ group }: any) => (
    <div data-testid={`group-${group.portfolioId}`}>
      {group.portfolioName} ({group.projects.length})
    </div>
  )
  MockPortfolioGroupSection.displayName = 'MockPortfolioGroupSection'
  return MockPortfolioGroupSection
})

function createProject(
  key: string,
  name: string,
  portfolioId: string,
  portfolioName: string,
  statusName: string = 'Active',
): ProjectListDto {
  return {
    id: `id-${key}`,
    key,
    name,
    status: { name: statusName, lifecyclePhase: 'Active' } as any,
    portfolio: { id: portfolioId, key: 1, name: portfolioName } as any,
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

describe('PortfolioGroupList', () => {
  beforeEach(() => {
    jest.useFakeTimers()
  })

  afterEach(() => {
    jest.useRealTimers()
  })

  const defaultProps = {
    isLoading: false,
    selectedProjectKey: null as string | null,
    selectedRoles: [] as number[],
    onSelectProject: jest.fn(),
  }

  it('renders loading spinner when loading', () => {
    const { container } = render(
      <PortfolioGroupList
        {...defaultProps}
        projects={undefined}
        isLoading={true}
      />,
    )

    expect(container.querySelector('.ant-spin')).toBeInTheDocument()
  })

  it('renders empty message when no projects', () => {
    render(
      <PortfolioGroupList {...defaultProps} projects={[]} />,
    )

    expect(screen.getByText('No projects found')).toBeInTheDocument()
  })

  it('groups projects by portfolio', () => {
    const projects = [
      createProject('P1', 'Alpha', 'port-1', 'Product Delivery'),
      createProject('P2', 'Beta', 'port-1', 'Product Delivery'),
      createProject('P3', 'Gamma', 'port-2', 'Data Analytics'),
    ]

    render(<PortfolioGroupList {...defaultProps} projects={projects} />)

    expect(screen.getByTestId('group-port-1')).toHaveTextContent(
      'Product Delivery (2)',
    )
    expect(screen.getByTestId('group-port-2')).toHaveTextContent(
      'Data Analytics (1)',
    )
  })

  it('sorts portfolio groups alphabetically', () => {
    const projects = [
      createProject('P1', 'Alpha', 'port-2', 'Zebra Portfolio'),
      createProject('P2', 'Beta', 'port-1', 'Alpha Portfolio'),
    ]

    const { container } = render(
      <PortfolioGroupList {...defaultProps} projects={projects} />,
    )

    const groups = container.querySelectorAll('[data-testid^="group-"]')
    expect(groups[0]).toHaveTextContent('Alpha Portfolio')
    expect(groups[1]).toHaveTextContent('Zebra Portfolio')
  })

  it('renders empty message when projects is undefined', () => {
    render(
      <PortfolioGroupList {...defaultProps} projects={undefined} />,
    )

    expect(screen.getByText('No projects found')).toBeInTheDocument()
  })

  it('renders search input when projects exist', () => {
    const projects = [
      createProject('P1', 'Alpha', 'port-1', 'Product Delivery'),
    ]

    render(<PortfolioGroupList {...defaultProps} projects={projects} />)

    expect(
      screen.getByPlaceholderText('Search projects...'),
    ).toBeInTheDocument()
  })

  it('does not render search input when no projects', () => {
    render(<PortfolioGroupList {...defaultProps} projects={[]} />)

    expect(
      screen.queryByPlaceholderText('Search projects...'),
    ).not.toBeInTheDocument()
  })

  it('filters projects by name when searching', async () => {
    const user = userEvent.setup({ advanceTimers: jest.advanceTimersByTime })
    const projects = [
      createProject('P1', 'Alpha', 'port-1', 'Product Delivery'),
      createProject('P2', 'Beta', 'port-1', 'Product Delivery'),
      createProject('P3', 'Gamma', 'port-2', 'Data Analytics'),
    ]

    render(<PortfolioGroupList {...defaultProps} projects={projects} />)

    await user.type(
      screen.getByPlaceholderText('Search projects...'),
      'Alpha',
    )
    act(() => jest.advanceTimersByTime(300))

    expect(screen.getByTestId('group-port-1')).toHaveTextContent(
      'Product Delivery (1)',
    )
    expect(screen.queryByTestId('group-port-2')).not.toBeInTheDocument()
  })

  it('filters case-insensitively', async () => {
    const user = userEvent.setup({ advanceTimers: jest.advanceTimersByTime })
    const projects = [
      createProject('P1', 'Alpha', 'port-1', 'Product Delivery'),
      createProject('P2', 'Beta', 'port-1', 'Product Delivery'),
    ]

    render(<PortfolioGroupList {...defaultProps} projects={projects} />)

    await user.type(
      screen.getByPlaceholderText('Search projects...'),
      'alpha',
    )
    act(() => jest.advanceTimersByTime(300))

    expect(screen.getByTestId('group-port-1')).toHaveTextContent(
      'Product Delivery (1)',
    )
  })

  it('shows no matching message when search has no results', async () => {
    const user = userEvent.setup({ advanceTimers: jest.advanceTimersByTime })
    const projects = [
      createProject('P1', 'Alpha', 'port-1', 'Product Delivery'),
    ]

    render(<PortfolioGroupList {...defaultProps} projects={projects} />)

    await user.type(
      screen.getByPlaceholderText('Search projects...'),
      'xyz',
    )
    act(() => jest.advanceTimersByTime(300))

    expect(screen.getByText('No matching projects')).toBeInTheDocument()
  })
})
