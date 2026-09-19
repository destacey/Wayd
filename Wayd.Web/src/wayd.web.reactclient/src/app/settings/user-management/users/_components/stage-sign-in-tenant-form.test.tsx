import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import StageSignInTenantForm from './stage-sign-in-tenant-form'

// Mock window.getComputedStyle for Ant Design Modal
Object.defineProperty(window, 'getComputedStyle', {
  value: () => ({
    getPropertyValue: () => '',
  }),
})

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => true }),
}))

// isValid is set from an async validation this Jest setup never delivers; the submit
// itself still runs the real validateFields and onSubmit.
jest.mock('@/src/hooks', () => {
  const actual = jest.requireActual('@/src/hooks')
  return {
    ...actual,
    useModalForm: (options: Parameters<typeof actual.useModalForm>[0]) => ({
      ...actual.useModalForm(options),
      isValid: true,
    }),
  }
})

const mockStageSignInTenant = jest.fn()
const mockUseGetEntraTenantIdsQuery = jest.fn()
jest.mock('@/src/store/features/user-management/users-api', () => ({
  useStageSignInTenantMutation: () => [
    mockStageSignInTenant,
    { isLoading: false },
  ],
  useGetEntraTenantIdsQuery: () => mockUseGetEntraTenantIdsQuery(),
}))

const renderForm = (currentTenantId?: string) =>
  render(
    <StageSignInTenantForm
      userId="user-1"
      userName="Morgan Lee"
      currentTenantId={currentTenantId}
      onFormComplete={jest.fn()}
      onFormCancel={jest.fn()}
    />,
  )

const arrangeTenants = (tenantIds: string[]) =>
  mockUseGetEntraTenantIdsQuery.mockReturnValue({
    data: tenantIds,
    isLoading: false,
  })

describe('StageSignInTenantForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockStageSignInTenant.mockResolvedValue({ data: undefined })
  })

  it('stages the only allowed tenant without asking for one', async () => {
    // Arrange
    arrangeTenants(['tenant-a'])
    const user = userEvent.setup()
    renderForm()
    expect(await screen.findByText('tenant-a')).toBeInTheDocument()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()

    // Act
    await act(async () => {
      await user.click(screen.getByRole('button', { name: 'Set Tenant' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockStageSignInTenant).toHaveBeenCalledWith({
        userId: 'user-1',
        tenantId: undefined,
      }),
    )
  })

  it('asks for the tenant when the Entra provider allows several', async () => {
    // Arrange
    arrangeTenants(['tenant-a', 'tenant-b'])

    // Act
    renderForm()

    // Assert
    expect(await screen.findByRole('combobox')).toBeInTheDocument()
  })

  it('submits the staged tenant already chosen when replacing it', async () => {
    // Arrange — the current tenant pre-selects, so resubmitting keeps it.
    arrangeTenants(['tenant-a', 'tenant-b'])
    const user = userEvent.setup()
    renderForm('tenant-b')
    await screen.findByRole('combobox')

    // Act
    await act(async () => {
      await user.click(screen.getByRole('button', { name: 'Set Tenant' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockStageSignInTenant).toHaveBeenCalledWith({
        userId: 'user-1',
        tenantId: 'tenant-b',
      }),
    )
  })

  it('blocks submitting until the tenants have loaded', async () => {
    // Arrange — submitting now would send no tenant, which a multi-tenant provider rejects.
    mockUseGetEntraTenantIdsQuery.mockReturnValue({
      data: undefined,
      isLoading: true,
    })

    // Act
    renderForm()

    // Assert
    expect(
      await screen.findByRole('button', { name: 'Set Tenant' }),
    ).toBeDisabled()
  })

  it('warns and blocks submitting when Microsoft Entra ID is not configured', async () => {
    // Arrange
    arrangeTenants([])

    // Act
    renderForm()

    // Assert
    expect(
      await screen.findByText(/Microsoft Entra ID isn't configured/),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Set Tenant' })).toBeDisabled()
  })
})
