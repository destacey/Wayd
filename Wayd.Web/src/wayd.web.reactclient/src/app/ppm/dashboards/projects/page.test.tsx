import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import ProjectsDashboardPage from './page'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

// The page is about which endpoint and which employee the queries are scoped
// to, so the mocks record the arguments and the tests assert on those.
let mockSearchParams = new URLSearchParams()
const mockReplace = jest.fn((url: string) => {
  mockSearchParams = new URLSearchParams(url.split('?')[1] ?? '')
})
const mockAuth = { employeeId: 'me' as string | null }
const mockProjectsQuery = jest.fn()
const mockPortfolioProjectsQuery = jest.fn()
const mockProgramProjectsQuery = jest.fn()
const mockPlanSummariesQuery = jest.fn()

const idle = {
  data: undefined,
  isLoading: false,
  error: undefined,
  refetch: jest.fn(),
}

jest.mock('next/navigation', () => ({
  usePathname: () => '/ppm/dashboards/projects',
  useRouter: () => ({ replace: mockReplace, push: jest.fn() }),
  useSearchParams: () => mockSearchParams,
}))

jest.mock('@/src/components/hoc', () => ({
  authorizePage: (component: unknown) => component,
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn() }),
}))

jest.mock('@/src/hooks', () => ({
  useDocumentTitle: jest.fn(),
  useDebounce: (value: unknown) => value,
  useRemainingHeight: () => [jest.fn(), 600],
  useLinkedEmployee: () => ({
    employeeId: mockAuth.employeeId,
    hasLinkedEmployee: mockAuth.employeeId !== null,
  }),
  useLocalStorageState: (_key: string, initial: unknown) =>
    jest.requireActual('react').useState(initial),
}))

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectsQuery: (...args: unknown[]) => mockProjectsQuery(...args),
  useGetProjectsPlanSummariesQuery: (...args: unknown[]) =>
    mockPlanSummariesQuery(...args),
}))

jest.mock('@/src/store/features/ppm/portfolios-api', () => ({
  useGetPortfolioProjectsQuery: (...args: unknown[]) =>
    mockPortfolioProjectsQuery(...args),
}))

jest.mock('@/src/store/features/ppm/programs-api', () => ({
  useGetProgramProjectsQuery: (...args: unknown[]) =>
    mockProgramProjectsQuery(...args),
}))

jest.mock('./_components/scope-bar', () => {
  const MockScopeBar = ({
    scope,
    onScopeChange,
  }: {
    scope: { kind: string; employeeId?: string | null }
    onScopeChange: (scope: unknown) => void
  }) => (
    <div>
      <span data-testid="scope">
        {scope.kind}:{scope.employeeId ?? ''}
      </span>
      <button
        type="button"
        onClick={() => onScopeChange({ kind: 'person', employeeId: 'ada' })}
      >
        pick ada
      </button>
      <button
        type="button"
        onClick={() =>
          onScopeChange({ kind: 'portfolio', portfolioId: 'port-1' })
        }
      >
        pick portfolio
      </button>
      <button type="button" onClick={() => onScopeChange({ kind: 'me' })}>
        back to me
      </button>
    </div>
  )
  MockScopeBar.displayName = 'MockScopeBar'
  return MockScopeBar
})

jest.mock('./_components/attention-tiles', () => {
  const MockTiles = () => <div data-testid="tiles" />
  MockTiles.displayName = 'MockAttentionTiles'
  return MockTiles
})

jest.mock('./_components/breakdown-strip', () => {
  const MockStrip = () => <div data-testid="breakdowns" />
  MockStrip.displayName = 'MockBreakdownStrip'
  return MockStrip
})

jest.mock('./_components/projects-dashboard-grid', () => {
  const MockGrid = ({
    employeeId,
    onViewChange,
  }: {
    employeeId: string | null
    onViewChange: (view: string) => void
  }) => (
    <div data-testid="list">
      {employeeId ?? 'none'}
      <button type="button" onClick={() => onViewChange('Card')}>
        show cards
      </button>
    </div>
  )
  MockGrid.displayName = 'MockProjectsDashboardGrid'
  return MockGrid
})

jest.mock('./_components/projects-dashboard-timeline', () => {
  const MockTimeline = () => <div data-testid="timeline" />
  MockTimeline.displayName = 'MockProjectsDashboardTimeline'
  return MockTimeline
})

jest.mock('./_components/projects-dashboard-cards', () => {
  const MockCards = () => <div data-testid="cards" />
  MockCards.displayName = 'MockProjectsDashboardCards'
  return MockCards
})

jest.mock('./_components/dashboard-toolbar', () => {
  const MockToolbar = ({
    onViewChange,
  }: {
    onViewChange: (view: string) => void
  }) => (
    <>
      <span data-testid="toolbar" />
      <button type="button" onClick={() => onViewChange('Timeline')}>
        show timeline
      </button>
    </>
  )
  MockToolbar.displayName = 'MockDashboardToolbar'
  return MockToolbar
})

jest.mock('@/src/app/ppm/_components/project-drawer', () => {
  const MockDrawer = () => null
  MockDrawer.displayName = 'MockProjectDrawer'
  return MockDrawer
})

jest.mock('@/src/components/common', () => ({
  PageTitle: ({ title }: { title: string }) => <h1>{title}</h1>,
  UnlinkedEmployeeAlert: () => null,
}))

jest.mock('@/src/components/common/basic-breadcrumb', () => {
  const MockBreadcrumb = () => null
  MockBreadcrumb.displayName = 'MockBreadcrumb'
  return MockBreadcrumb
})

const lastCall = (mock: jest.Mock) =>
  mock.mock.calls[mock.mock.calls.length - 1]
/** The person-scoped call to the general list: the one carrying a role filter. */
const lastPersonCall = () =>
  [...mockProjectsQuery.mock.calls].reverse().find(([args]) => 'role' in args)!

const aProject = {
  id: 'p1',
  key: 'P1',
  name: 'One',
  status: { id: 2, name: 'Active', lifecycleCategory: 'Active' },
  portfolio: { id: 'port', key: 1, name: 'Portfolio' },
}

describe('ProjectsDashboardPage', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockSearchParams = new URLSearchParams()
    mockAuth.employeeId = 'me'
    mockProjectsQuery.mockReturnValue({ ...idle, data: [] })
    mockPortfolioProjectsQuery.mockReturnValue(idle)
    mockProgramProjectsQuery.mockReturnValue(idle)
    mockPlanSummariesQuery.mockReturnValue({ data: {} })
  })

  it('defaults to the Me scope and lets the server resolve the employee', () => {
    // Arrange / Act
    render(<ProjectsDashboardPage />)

    // Assert — no employeeId on the wire, every role requested, list scoped to me
    expect(screen.getByTestId('scope')).toHaveTextContent('me:')
    const [args, options] = lastPersonCall()
    expect(args.employeeId).toBeUndefined()
    expect(args.role).toEqual([1, 2, 3, 4, 5])
    expect(options.skip).toBe(false)
    expect(screen.getByTestId('list')).toHaveTextContent('me')
    expect(screen.queryByTestId('breakdowns')).not.toBeInTheDocument()
  })

  it('reads the person scope from the URL and passes that employee to the queries', () => {
    // Arrange
    mockSearchParams = new URLSearchParams('employee=ada')
    mockProjectsQuery.mockReturnValue({ ...idle, data: [aProject] })

    // Act
    render(<ProjectsDashboardPage />)

    // Assert
    expect(screen.getByTestId('scope')).toHaveTextContent('person:ada')
    expect(lastPersonCall()[0].employeeId).toBe('ada')
    expect(mockPlanSummariesQuery).toHaveBeenLastCalledWith(
      expect.objectContaining({
        projectIds: ['p1'],
        employeeId: 'ada',
        allTasks: false,
      }),
      { skip: false },
    )
    expect(screen.getByTestId('list')).toHaveTextContent('ada')
  })

  it("loads a portfolio scope from the portfolio's own list and counts every task", () => {
    // Arrange
    mockSearchParams = new URLSearchParams('portfolio=port-1')
    mockPortfolioProjectsQuery.mockReturnValue({ ...idle, data: [aProject] })

    // Act
    render(<ProjectsDashboardPage />)

    // Assert
    expect(lastCall(mockPortfolioProjectsQuery)).toEqual([
      { portfolioIdOrKey: 'port-1', status: [5, 2] },
      { skip: false },
    ])
    expect(lastPersonCall()[1].skip).toBe(true)
    expect(mockPlanSummariesQuery).toHaveBeenLastCalledWith(
      expect.objectContaining({ projectIds: ['p1'], allTasks: true }),
      { skip: false },
    )
    // No person: the Role column has nobody to describe, and breakdowns appear
    expect(screen.getByTestId('list')).toHaveTextContent('none')
    expect(screen.getByTestId('breakdowns')).toBeInTheDocument()
  })

  it('loads every project for the all scope without a role filter', () => {
    // Arrange
    mockSearchParams = new URLSearchParams('scope=all')

    // Act
    render(<ProjectsDashboardPage />)

    // Assert
    const everything = [...mockProjectsQuery.mock.calls]
      .reverse()
      .find(([args]) => !('role' in args))!
    expect(everything).toEqual([{ status: [5, 2] }, { skip: false }])
    expect(lastPersonCall()[1].skip).toBe(true)
  })

  it('writes a scope change to the URL rather than to local state', async () => {
    // Arrange
    render(<ProjectsDashboardPage />)

    // Act
    await userEvent.click(screen.getByText('pick ada'))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/ppm/dashboards/projects?employee=ada',
    )

    // Act — a record scope swaps the parameter, and Me drops it
    await userEvent.click(screen.getByText('pick portfolio'))
    expect(mockReplace).toHaveBeenLastCalledWith(
      '/ppm/dashboards/projects?portfolio=port-1',
    )
    await userEvent.click(screen.getByText('back to me'))
    expect(mockReplace).toHaveBeenLastCalledWith('/ppm/dashboards/projects')
  })

  it('skips the projects query until a person is chosen in person scope', () => {
    // Arrange — no employee chosen yet
    mockSearchParams = new URLSearchParams('employee=')

    // Act
    render(<ProjectsDashboardPage />)

    // Assert
    expect(screen.getByTestId('scope')).toHaveTextContent('person:')
    expect(lastPersonCall()[1].skip).toBe(true)
  })

  it('falls back to person scope for an account with no linked employee', () => {
    // Arrange
    mockAuth.employeeId = null

    // Act
    render(<ProjectsDashboardPage />)

    // Assert
    expect(screen.getByTestId('scope')).toHaveTextContent('person:')
    expect(lastPersonCall()[1].skip).toBe(true)
  })

  it('switches the body to cards from the toolbar', async () => {
    // Arrange
    render(<ProjectsDashboardPage />)

    // Act
    // The List view carries the switch in the grid's toolbar and shows no
    // dashboard toolbar of its own; the other views bring the toolbar back.
    expect(screen.queryByTestId('toolbar')).not.toBeInTheDocument()
    await userEvent.click(screen.getByText('show cards'))

    // Assert
    expect(screen.getByTestId('cards')).toBeInTheDocument()
    expect(screen.getByTestId('toolbar')).toBeInTheDocument()
    expect(screen.queryByTestId('list')).not.toBeInTheDocument()

    await userEvent.click(screen.getByText('show timeline'))
    expect(screen.getByTestId('timeline')).toBeInTheDocument()
    expect(screen.queryByTestId('cards')).not.toBeInTheDocument()
  })
})
