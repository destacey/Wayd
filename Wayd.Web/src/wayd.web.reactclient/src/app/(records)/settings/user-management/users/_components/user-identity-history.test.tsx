import { render, screen } from '@testing-library/react'
import { UserIdentityDto } from '@/src/services/wayd-api'
import UserIdentityHistory from './user-identity-history'

const mockHistoryQuery = jest.fn()

jest.mock('@/src/store/features/user-management/users-api', () => ({
  useGetUserIdentityHistoryQuery: (...args: unknown[]) =>
    mockHistoryQuery(...args),
}))

const identity = (overrides: Partial<UserIdentityDto> = {}): UserIdentityDto =>
  ({
    id: 'f2b8c400-0000-0000-0000-000000000001',
    provider: 'MicrosoftEntraId',
    providerTenantId: '00000000-1111-2222-3333-444444444444',
    providerSubject: 'abcdefghijklmnop',
    isActive: true,
    linkedAt: new Date('2026-01-15T09:30:00Z'),
    ...overrides,
  }) as UserIdentityDto

const USER_ID = 'e1a7b300-0000-0000-0000-000000000001'

describe('UserIdentityHistory', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockHistoryQuery.mockReturnValue({
      data: [identity()],
      isLoading: false,
      error: undefined,
    })
  })

  it('asks for the history of the user it was given', () => {
    // Arrange / Act
    render(<UserIdentityHistory userId={USER_ID} />)

    // Assert
    expect(mockHistoryQuery).toHaveBeenCalledWith(USER_ID)
  })

  it('names the provider rather than showing its id', () => {
    // Arrange / Act — "MicrosoftEntraId" is not something to show a person
    render(<UserIdentityHistory userId={USER_ID} />)

    // Assert
    expect(screen.getByText('Microsoft Entra ID')).toBeInTheDocument()
  })

  it('truncates a long provider subject', () => {
    // Arrange / Act — a subject is long and opaque; the full value stays on
    // the tooltip and the clipboard
    render(<UserIdentityHistory userId={USER_ID} />)

    // Assert
    expect(screen.getByText('abcdefgh…')).toBeInTheDocument()
  })

  it('shows a short subject in full', () => {
    // Arrange
    mockHistoryQuery.mockReturnValue({
      data: [identity({ providerSubject: 'short' })],
      isLoading: false,
      error: undefined,
    })

    // Act
    render(<UserIdentityHistory userId={USER_ID} />)

    // Assert
    expect(screen.getByText('short')).toBeInTheDocument()
  })

  it('marks an inactive binding', () => {
    // Arrange — a past identity is the point of an audit trail
    mockHistoryQuery.mockReturnValue({
      data: [identity({ isActive: false, unlinkReason: 'TenantMigration' })],
      isLoading: false,
      error: undefined,
    })

    // Act
    render(<UserIdentityHistory userId={USER_ID} />)

    // Assert
    expect(screen.getByText('Inactive')).toBeInTheDocument()
    expect(screen.getByText('Tenant Migration')).toBeInTheDocument()
  })

  it('says so when there is no history', () => {
    // Arrange
    mockHistoryQuery.mockReturnValue({
      data: [],
      isLoading: false,
      error: undefined,
    })

    // Act
    render(<UserIdentityHistory userId={USER_ID} />)

    // Assert
    expect(
      screen.getByText('No identity history available.'),
    ).toBeInTheDocument()
  })

  it('reports a failed load rather than an empty table', () => {
    // Arrange
    mockHistoryQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('nope'),
    })

    // Act
    render(<UserIdentityHistory userId={USER_ID} />)

    // Assert
    expect(
      screen.getByText('Failed to load identity history.'),
    ).toBeInTheDocument()
  })
})
