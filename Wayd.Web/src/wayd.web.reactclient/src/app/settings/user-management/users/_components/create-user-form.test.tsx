import { render, screen } from '@testing-library/react'
import CreateUserForm from './create-user-form'

// Mock window.getComputedStyle for Ant Design Modal
Object.defineProperty(window, 'getComputedStyle', {
  value: () => ({
    getPropertyValue: () => '',
  }),
})

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: vi.fn(), error: vi.fn() }),
}))

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => true }),
}))

const mockUseGetEntraTenantIdsQuery = vi.fn()
vi.mock('@/src/store/features/user-management/users-api', () => ({
  useCreateUserMutation: () => [vi.fn(), { isLoading: false }],
  useGetUsersQuery: () => ({ data: [] }),
  useGetEntraTenantIdsQuery: (...args: unknown[]) =>
    mockUseGetEntraTenantIdsQuery(...args),
}))

vi.mock('@/src/store/features/user-management/roles-api', () => ({
  useGetRolesQuery: () => ({ data: [], isLoading: false }),
}))

vi.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeesQuery: () => ({ data: [], isLoading: false }),
}))

const renderForm = () =>
  render(<CreateUserForm onFormCreate={vi.fn()} onFormCancel={vi.fn()} />)

const arrangeTenants = (tenantIds: string[]) =>
  mockUseGetEntraTenantIdsQuery.mockImplementation(
    (_: unknown, options?: { skip?: boolean }) => ({
      data: options?.skip ? undefined : tenantIds,
    }),
  )

describe('CreateUserForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('asks for the sign-in tenant when the Entra provider allows several', async () => {
    // Arrange
    arrangeTenants(['tenant-a', 'tenant-b'])

    // Act
    renderForm()

    // Assert
    expect(await screen.findByText('Sign-in Tenant')).toBeInTheDocument()
  })

  it('does not ask for a tenant when the Entra provider allows only one', async () => {
    // Arrange
    arrangeTenants(['tenant-a'])

    // Act
    renderForm()

    // Assert
    await screen.findByText('Login Provider')
    expect(screen.queryByText('Sign-in Tenant')).not.toBeInTheDocument()
    expect(screen.queryByText(/isn't configured/)).not.toBeInTheDocument()
  })

  it('warns when Microsoft Entra ID is not configured', async () => {
    // Arrange
    arrangeTenants([])

    // Act
    renderForm()

    // Assert
    expect(
      await screen.findByText(/Microsoft Entra ID isn't configured/),
    ).toBeInTheDocument()
    expect(screen.queryByText('Sign-in Tenant')).not.toBeInTheDocument()
  })
})
