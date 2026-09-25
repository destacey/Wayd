import { fireEvent, render, screen } from '@testing-library/react'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import ProjectDrawer from './project-drawer'

jest.mock('next/link', () => {
  const MockLink = ({ href, children }: any) => <a href={href}>{children}</a>
  MockLink.displayName = 'MockLink'
  return MockLink
})

// antd's Drawer reaches for Next's server-side AsyncLocalStorage, which jsdom
// has no runtime for — a bare <Drawer> fails to render on its own. Only the
// Drawer is stubbed, so the facts markup under test stays real.
jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  const MockDrawer = ({ title, extra, open, children }: any) =>
    open ? (
      <div>
        <div>{title}</div>
        <div data-testid="drawer-extra">{extra}</div>
        {children}
      </div>
    ) : null
  MockDrawer.displayName = 'MockDrawer'
  return { ...actual, Drawer: MockDrawer }
})

const mockProject = jest.fn()

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectQuery: (...args: unknown[]) => mockProject(...args),
}))

const mockClaims = { granted: true }

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => mockClaims.granted }),
}))

jest.mock(
  '@/src/app/ppm/projects/_components/create-project-health-check-form',
  () => ({
    __esModule: true,
    default: ({ projectId }: { projectId: string }) => (
      <div data-testid="health-check-form">{projectId}</div>
    ),
  }),
)

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn() }),
}))

jest.mock('@/src/components/common/links/links-card', () => ({
  __esModule: true,
  default: () => <div data-testid="links-card" />,
}))

jest.mock(
  '@/src/app/ppm/projects/_components/scoring/project-score-card',
  () => ({
    __esModule: true,
    default: () => <div data-testid="score-card" />,
  }),
)

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeByIdQuery: () => ({
    data: undefined,
    isLoading: false,
    isError: false,
  }),
}))

const person = (id: string, key: number, name: string) => ({ id, key, name })

const DANA = person('11111111-0000-4000-a000-000000000001', 1042, 'Dana Reid')

const baseProject = {
  id: 'aaaaaaaa-0000-4000-a000-000000000001',
  key: 'PRJ-1',
  name: 'Atlas Rollout',
  description: '',
  businessCase: '',
  expectedBenefits: '',
  status: { id: '1', name: 'Active' },
  expenditureCategory: { id: '1', key: 1, name: 'Capex' },
  portfolio: { id: 'p1', key: 7, name: 'Core Platform' },
  projectSponsors: [],
  projectOwners: [DANA],
  projectManagers: [],
  projectMembers: [],
  strategicThemes: [],
  strategicInitiatives: [],
  stages: [],
  canManageProject: false,
} as unknown as ProjectDetailsDto

const renderDrawer = (overrides: Partial<ProjectDetailsDto> = {}) => {
  mockProject.mockReturnValue({
    data: { ...baseProject, ...overrides },
    isLoading: false,
    error: undefined,
  })

  return render(
    <ProjectDrawer projectKey="PRJ-1" drawerOpen onDrawerClose={jest.fn()} />,
  )
}

describe('ProjectDrawer', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockClaims.granted = true
  })

  it('offers a health check from the header to someone who can manage the project', () => {
    // Arrange
    renderDrawer({ canManageProject: true })

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Create Health Check' }))

    // Assert — the form opens for this project
    expect(screen.getByTestId('health-check-form')).toHaveTextContent(
      baseProject.id,
    )
  })

  it('withholds the health check button without leadership or the permission', () => {
    // Arrange / Act — leadership missing
    const { unmount } = renderDrawer({ canManageProject: false })

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Create Health Check' }),
    ).not.toBeInTheDocument()

    // Arrange / Act — leadership present, permission missing
    unmount()
    mockClaims.granted = false
    renderDrawer({ canManageProject: true })
    expect(
      screen.queryByRole('button', { name: 'Create Health Check' }),
    ).not.toBeInTheDocument()
  })

  it('groups the facts the way the record page does', () => {
    // Arrange / Act
    renderDrawer()

    // Assert
    expect(screen.getByText('Roles')).toBeInTheDocument()
    expect(screen.getByText('Relationships')).toBeInTheDocument()
  })

  it('links a role holder to their employee record rather than naming them flatly', () => {
    // Arrange / Act
    renderDrawer()

    // Assert
    expect(screen.getByRole('link', { name: 'Dana Reid' })).toHaveAttribute(
      'href',
      '/organizations/employees/1042',
    )
  })

  it('puts the portfolio in the relationships group', () => {
    // Arrange / Act
    renderDrawer()

    // Assert
    expect(screen.getByRole('link', { name: 'Core Platform' })).toHaveAttribute(
      'href',
      '/ppm/portfolios/7',
    )
  })

  it('links each strategic initiative separately', () => {
    // Arrange / Act
    renderDrawer({
      strategicInitiatives: [
        { id: 's2', key: 12, name: 'Zero Downtime' },
        { id: 's1', key: 11, name: 'Cost Reduction' },
      ],
    } as unknown as Partial<ProjectDetailsDto>)

    // Assert
    const links = screen
      .getAllByRole('link')
      .map((l) => l.textContent)
      .filter((t) => t === 'Cost Reduction' || t === 'Zero Downtime')
    expect(links).toEqual(['Cost Reduction', 'Zero Downtime'])
  })
})
