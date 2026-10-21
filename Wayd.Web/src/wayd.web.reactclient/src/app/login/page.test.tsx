import { fireEvent, render, screen, waitFor } from '@testing-library/react'
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

jest.mock('@/src/components/contexts/auth/oidc-client-registry', () => ({
  signinRedirect: jest.fn(),
  completeSignin: jest.fn(),
  signinSilent: jest.fn().mockResolvedValue(null),
  getChosenProviderName: jest.fn().mockReturnValue(null),
  clearChosenProvider: jest.fn(),
  getLastProviderName: jest.fn().mockReturnValue(null),
  clearLastProvider: jest.fn(),
}))

const mockNavigate = jest.fn()
const mockTakeReturnUrl = jest.fn()

jest.mock('@/src/utils/window-utils', () => ({
  navigateWithFullReload: (url: string) => mockNavigate(url),
}))

jest.mock('@/src/utils/return-url', () => ({
  takeReturnUrl: () => mockTakeReturnUrl(),
}))

// Controlled return for the auth providers hook
const mockUseGetAuthProvidersQuery = jest.fn()

jest.mock('@/src/store/features/common/auth-providers-api', () => ({
  useGetAuthProvidersQuery: () => mockUseGetAuthProvidersQuery(),
}))

// Imports after mocks
import LoginPage from './page'
import { getAuthClient, storeAuth } from '@/src/services/clients'
import {
  completeSignin,
  getChosenProviderName,
  getLastProviderName,
  signinSilent,
} from '@/src/components/contexts/auth/oidc-client-registry'

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

describe('LoginPage return to the requested page', () => {
  const RETURN_PATH = '/oauth/consent?request=handle-abc123'

  beforeEach(() => {
    jest.clearAllMocks()
    window.history.replaceState(null, '', '/oauth/consent?request=handle-abc123')
    // clearAllMocks keeps return values, and each test sets its own sign-in path.
    jest.mocked(getLastProviderName).mockReturnValue(null)
    jest.mocked(getChosenProviderName).mockReturnValue(null)
  })

  afterEach(() => {
    window.history.replaceState(null, '', '/')
  })

  it.each([
    [RETURN_PATH, RETURN_PATH],
    [null, '/'],
  ])(
    'after a local sign-in, goes to the stored page (%s), else home',
    async (stored, expected) => {
      // Arrange
      mockUseGetAuthProvidersQuery.mockReturnValue({
        data: { local: true, oidc: [] },
        isLoading: false,
      })
      jest.mocked(getAuthClient).mockReturnValue({
        login: jest.fn().mockResolvedValue({ token: 't' }),
      } as never)
      mockTakeReturnUrl.mockReturnValue(stored)
      render(<LoginPage />)

      // Act
      fireEvent.change(screen.getByPlaceholderText('Email'), {
        target: { value: 'pat@acme.example' },
      })
      fireEvent.change(screen.getByPlaceholderText('Password'), {
        target: { value: 'correct horse' },
      })
      fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

      // Assert
      await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith(expected))
      expect(storeAuth).toHaveBeenCalledWith({ token: 't' })
    },
  )

  it('after a silent sign-in, goes to the stored page', async () => {
    // Arrange
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: false, oidc: [entraProvider] },
      isLoading: false,
    })
    jest.mocked(getLastProviderName).mockReturnValue(entraProvider.name)
    jest
      .mocked(signinSilent)
      .mockResolvedValue({ access_token: 'entra-token' } as never)
    jest.mocked(getAuthClient).mockReturnValue({
      exchange: jest.fn().mockResolvedValue({ token: 't' }),
    } as never)
    mockTakeReturnUrl.mockReturnValue(RETURN_PATH)

    // Act
    render(<LoginPage />)

    // Assert
    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith(RETURN_PATH))
    expect(storeAuth).toHaveBeenCalledWith({ token: 't' })
  })

  it('after a Microsoft sign-in callback, goes to the stored page', async () => {
    // Arrange
    window.history.replaceState(null, '', '/login?code=c1&state=s1')
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: false, oidc: [entraProvider] },
      isLoading: false,
    })
    jest.mocked(getChosenProviderName).mockReturnValue(entraProvider.name)
    jest
      .mocked(completeSignin)
      .mockResolvedValue({ access_token: 'entra-token' } as never)
    jest.mocked(getAuthClient).mockReturnValue({
      exchange: jest.fn().mockResolvedValue({ token: 't' }),
    } as never)
    mockTakeReturnUrl.mockReturnValue(RETURN_PATH)

    // Act
    render(<LoginPage />)

    // Assert
    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith(RETURN_PATH))
    expect(storeAuth).toHaveBeenCalledWith({ token: 't' })
  })
})
