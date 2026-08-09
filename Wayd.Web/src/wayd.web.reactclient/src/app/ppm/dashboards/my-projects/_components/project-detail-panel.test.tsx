import { render, screen } from '@testing-library/react'
import ProjectDetailPanel from './project-detail-panel'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectQuery: jest.fn(),
  useGetProjectPlanTreeQuery: jest.fn(),
  useGetProjectPlanSummaryQuery: jest.fn(),
}))

jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({
    token: {
      colorError: '#ff4d4f',
      colorWarning: '#faad14',
    },
  }),
}))

// Mock child components to isolate panel logic
jest.mock('./project-detail-header', () => {
  const MockDetailHeader = ({ project }: any) => (
    <div data-testid="detail-header">{project.name}</div>
  )
  MockDetailHeader.displayName = 'MockDetailHeader'
  return MockDetailHeader
})

jest.mock('./project-plan-view', () => {
  const MockPlanView = ({ projectKey }: any) => (
    <div data-testid="plan-view">{projectKey}</div>
  )
  MockPlanView.displayName = 'MockPlanView'
  return MockPlanView
})

jest.mock('@/src/app/ppm/_components/phase-timeline', () => {
  const MockPhaseTimeline = ({ phases }: any) => (
    <div data-testid="phase-timeline">{phases.length} phases</div>
  )
  MockPhaseTimeline.displayName = 'MockPhaseTimeline'
  return MockPhaseTimeline
})

jest.mock('@/src/app/ppm/projects/_components/project-task-metrics', () => {
  const MockTaskMetrics = ({ projectKey }: any) => (
    <div data-testid="task-metrics">{projectKey}</div>
  )
  MockTaskMetrics.displayName = 'MockTaskMetrics'
  return MockTaskMetrics
})

import { useGetProjectQuery } from '@/src/store/features/ppm/projects-api'

const mockUseGetProjectQuery = useGetProjectQuery as jest.Mock

function createProjectDetails(overrides?: any) {
  return {
    id: 'proj-1',
    key: 'PROJ1',
    name: 'Test Project',
    description: '',
    status: { name: 'Active', lifecycleCategory: 'Active' },
    expenditureCategory: { id: 1, name: 'Capital' },
    portfolio: { id: 'port-1', key: 1, name: 'Portfolio' },
    projectSponsors: [],
    projectOwners: [],
    projectManagers: [],
    projectMembers: [],
    strategicThemes: [],
    phases: [{ id: 'ph-1', name: 'Phase 1', order: 1, status: { id: 1, name: 'Not Started' }, progress: 0 }],
    projectLifecycle: { id: 'lc-1', key: 1, name: 'Standard' },
    ...overrides,
  }
}

describe('ProjectDetailPanel', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders empty state when no project is selected', () => {
    render(<ProjectDetailPanel projectKey={null} />)

    expect(
      screen.getByText('Select a project to view details'),
    ).toBeInTheDocument()
  })

  it('renders loading skeleton when project is loading', () => {
    mockUseGetProjectQuery.mockReturnValue({
      data: undefined,
      isLoading: true,
    })

    const { container } = render(
      <ProjectDetailPanel projectKey="PROJ1" />,
    )

    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders not found when project data is null', () => {
    mockUseGetProjectQuery.mockReturnValue({
      data: null,
      isLoading: false,
    })

    render(<ProjectDetailPanel projectKey="PROJ1" />)

    expect(screen.getByText('Project not found')).toBeInTheDocument()
  })

  it('renders detail header with project data', () => {
    mockUseGetProjectQuery.mockReturnValue({
      data: createProjectDetails(),
      isLoading: false,
    })

    render(<ProjectDetailPanel projectKey="PROJ1" />)

    expect(screen.getByTestId('detail-header')).toHaveTextContent(
      'Test Project',
    )
  })

  it('renders phase timeline when phases exist', () => {
    mockUseGetProjectQuery.mockReturnValue({
      data: createProjectDetails(),
      isLoading: false,
    })

    render(<ProjectDetailPanel projectKey="PROJ1" />)

    expect(screen.getByTestId('phase-timeline')).toHaveTextContent(
      '1 phases',
    )
  })

  it('renders task metrics', () => {
    mockUseGetProjectQuery.mockReturnValue({
      data: createProjectDetails(),
      isLoading: false,
    })

    render(<ProjectDetailPanel projectKey="PROJ1" />)

    expect(screen.getByTestId('task-metrics')).toHaveTextContent('PROJ1')
  })

  it('renders project plan view', () => {
    mockUseGetProjectQuery.mockReturnValue({
      data: createProjectDetails(),
      isLoading: false,
    })

    render(<ProjectDetailPanel projectKey="PROJ1" />)

    expect(screen.getByTestId('plan-view')).toHaveTextContent('PROJ1')
  })

  it('renders WaydEmpty when no lifecycle is assigned', () => {
    mockUseGetProjectQuery.mockReturnValue({
      data: createProjectDetails({ projectLifecycle: null }),
      isLoading: false,
    })

    render(<ProjectDetailPanel projectKey="PROJ1" />)

    expect(
      screen.getByText(
        /No project plan defined/,
      ),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('plan-view')).not.toBeInTheDocument()
    expect(screen.queryByTestId('task-metrics')).not.toBeInTheDocument()
  })

  it('hides phase timeline when no phases', () => {
    mockUseGetProjectQuery.mockReturnValue({
      data: createProjectDetails({ phases: [] }),
      isLoading: false,
    })

    render(<ProjectDetailPanel projectKey="PROJ1" />)

    expect(screen.queryByTestId('phase-timeline')).not.toBeInTheDocument()
  })
})
