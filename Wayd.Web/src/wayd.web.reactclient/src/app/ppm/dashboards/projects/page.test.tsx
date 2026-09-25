import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import ProjectsDashboardPage from './page'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

// The page is about which employee the queries are scoped to, so the mocks
// record the arguments and the tests assert on those.
let mockSearchParams = new URLSearchParams()
const mockReplace = jest.fn((url: string) => {
  mockSearchParams = new URLSearchParams(url.split('?')[1] ?? '')
})
const mockAuth = { employeeId: 'me' as string | null }
const mockProjectsQuery = jest.fn()
const mockPlanSummariesQuery = jest.fn()

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

jest.mock('./_components/projects-dashboard-list', () => {
  const MockList = ({ employeeId }: { employeeId: string | null }) => (
    <div data-testid="list">{employeeId ?? 'none'}</div>
  )
  MockList.displayName = 'MockProjectsDashboardList'
  return MockList
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

const lastProjectsArgs = () =>
  mockProjectsQuery.mock.calls[mockProjectsQuery.mock.calls.length - 1]

describe('ProjectsDashboardPage', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockSearchParams = new URLSearchParams()
    mockAuth.employeeId = 'me'
    mockProjectsQuery.mockReturnValue({
      data: [],
      isLoading: false,
      error: undefined,
      refetch: jest.fn(),
    })
    mockPlanSummariesQuery.mockReturnValue({ data: {} })
  })

  it('defaults to the Me scope and lets the server resolve the employee', () => {
    // Arrange / Act
    render(<ProjectsDashboardPage />)

    // Assert — no employeeId on the wire, every role requested, list scoped to me
    expect(screen.getByTestId('scope')).toHaveTextContent('me:')
    const [args, options] = lastProjectsArgs()
    expect(args.employeeId).toBeUndefined()
    expect(args.role).toEqual([1, 2, 3, 4, 5])
    expect(options.skip).toBe(false)
    expect(screen.getByTestId('list')).toHaveTextContent('me')
  })

  it('reads the person scope from the URL and passes that employee to the queries', () => {
    // Arrange
    mockSearchParams = new URLSearchParams('employee=ada')
    mockProjectsQuery.mockReturnValue({
      data: [
        {
          id: 'p1',
          key: 'P1',
          name: 'One',
          status: { id: 2, name: 'Active', lifecycleCategory: 'Active' },
          portfolio: { id: 'port', key: 1, name: 'Portfolio' },
        },
      ],
      isLoading: false,
      refetch: jest.fn(),
    })

    // Act
    render(<ProjectsDashboardPage />)

    // Assert
    expect(screen.getByTestId('scope')).toHaveTextContent('person:ada')
    expect(lastProjectsArgs()[0].employeeId).toBe('ada')
    expect(mockPlanSummariesQuery).toHaveBeenLastCalledWith(
      expect.objectContaining({ projectIds: ['p1'], employeeId: 'ada' }),
      { skip: false },
    )
    expect(screen.getByTestId('list')).toHaveTextContent('ada')
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

    // Act — returning to Me drops the parameter
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
    expect(lastProjectsArgs()[1].skip).toBe(true)
  })

  it('falls back to person scope for an account with no linked employee', () => {
    // Arrange
    mockAuth.employeeId = null

    // Act
    render(<ProjectsDashboardPage />)

    // Assert
    expect(screen.getByTestId('scope')).toHaveTextContent('person:')
    expect(lastProjectsArgs()[1].skip).toBe(true)
  })
})
