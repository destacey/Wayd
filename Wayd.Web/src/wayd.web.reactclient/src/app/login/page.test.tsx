import { render, screen, waitFor } from '@testing-library/react'
import '@testing-library/jest-dom'

// --- Mocks ---

jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: jest.fn() }),
}))

jest.mock('@/src/services/clients', () => ({
  isAuthActive: () => false,
  getAuthClient: jest.fn(),
  setRememberMe: jest.fn(),
  storeAuth: jest.fn(),
}))

// Mocked beneath the real oidc-client-registry, so a test can tell whether the
// page builds a client for an identity provider.
jest.mock('oidc-client-ts', () => ({
  UserManager: jest.fn().mockImplementation(() => ({
    signinRedirect: jest.fn(),
    signinRedirectCallback: jest.fn(),
    signinSilent: jest.fn(),
    signoutRedirect: jest.fn(),
  })),
  WebStorageStateStore: jest.fn(),
}))

// Controlled return for the auth providers hook
const mockUseGetAuthProvidersQuery = jest.fn()

jest.mock('@/src/store/features/common/auth-providers-api', () => ({
  useGetAuthProvidersQuery: () => mockUseGetAuthProvidersQuery(),
}))

// Imports after mocks
import LoginPage from './page'
import { UserManager } from 'oidc-client-ts'

const entraProvider = {
  name: 'MicrosoftEntraId',
  displayName: 'Microsoft Entra ID',
  providerType: 'MicrosoftEntraId',
  authority: 'https://login.microsoftonline.com/common/v2.0',
  clientId: 'test-client-id',
  scopes: ['openid', 'profile'],
}

describe('LoginPage provider gating', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    // The login page reads window.location.href to detect a redirect callback
    // (?code= param). jsdom sets href to 'about:blank' by default which means
    // new URL(window.location.href).searchParams.has('code') === false — that's
    // exactly what we want for the "not a redirect" case.
  })

  afterEach(() => {
    // jest.setup.ts replaces localStorage with jest.fn() stubs; clearAllMocks
    // keeps a test's mockImplementation, so it would leak into the next test.
    jest.mocked(localStorage.getItem).mockReset()
  })

  it('hides OIDC buttons and shows local form when no OIDC providers', async () => {
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: true, oidc: [] },
      isLoading: false,
    })

    render(<LoginPage />)

    await waitFor(() => {
      expect(screen.queryByText('Microsoft Entra ID')).not.toBeInTheDocument()
    })

    expect(screen.getByPlaceholderText('Email')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('Password')).toBeInTheDocument()
  })

  it('shows OIDC provider buttons when providers are returned', async () => {
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: false, oidc: [entraProvider] },
      isLoading: false,
    })

    render(<LoginPage />)

    await waitFor(() => {
      expect(screen.getByText('Microsoft Entra ID')).toBeInTheDocument()
    })
  })

  it('shows both tabs when OIDC providers and local are enabled', async () => {
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: true, oidc: [entraProvider] },
      isLoading: false,
    })

    render(<LoginPage />)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Email & Password' })).toBeInTheDocument()
    })
  })

  it('defaults to OIDC tab when providers are available', async () => {
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: true, oidc: [entraProvider] },
      isLoading: false,
    })

    render(<LoginPage />)

    await waitFor(() => {
      expect(screen.getByText('Microsoft Entra ID')).toBeInTheDocument()
    })
    // Local form should not be visible initially
    expect(screen.queryByPlaceholderText('Email')).not.toBeInTheDocument()
  })

  it('contacts no identity provider on load, even after an earlier SSO sign-in', async () => {
    // Arrange
    jest
      .mocked(localStorage.getItem)
      .mockImplementation((key) =>
        key === 'wayd.oidc.lastProvider' ? entraProvider.name : null,
      )
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: true, oidc: [entraProvider] },
      isLoading: false,
    })

    // Act
    render(<LoginPage />)

    // Assert
    await waitFor(() => {
      expect(screen.getByText('Microsoft Entra ID')).toBeInTheDocument()
    })
    expect(screen.queryByText('Completing sign-in…')).not.toBeInTheDocument()
    expect(UserManager).not.toHaveBeenCalled()
  })

  it('shows local form while the providers query is loading', () => {
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: undefined,
      isLoading: true,
    })

    render(<LoginPage />)

    expect(screen.queryByText('Microsoft Entra ID')).not.toBeInTheDocument()
    expect(screen.getByPlaceholderText('Email')).toBeInTheDocument()
  })

  it('keeps OIDC buttons hidden when providers query fails (local fallback)', async () => {
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    render(<LoginPage />)

    await waitFor(() => {
      expect(screen.queryByText('Microsoft Entra ID')).not.toBeInTheDocument()
    })

    expect(screen.getByPlaceholderText('Email')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('Password')).toBeInTheDocument()
  })
})
