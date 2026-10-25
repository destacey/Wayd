import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
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
import { UserManager } from 'oidc-client-ts'
import { getAuthClient, storeAuth } from '@/src/services/clients'
import { ApiException } from '@/src/services/wayd-api'

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

describe('LoginPage return to the requested page', () => {
  const RETURN_PATH = '/oauth/consent?request=handle-abc123'

  beforeEach(() => {
    jest.clearAllMocks()
    window.history.replaceState(null, '', '/oauth/consent?request=handle-abc123')
  })

  afterEach(() => {
    window.history.replaceState(null, '', '/')
    // jest.setup.ts replaces sessionStorage with jest.fn() stubs; clearAllMocks
    // keeps a test's mockImplementation, so it would leak into the next test.
    jest.mocked(sessionStorage.getItem).mockReset()
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

  it('after a Microsoft sign-in callback, goes to the stored page', async () => {
    // Arrange
    window.history.replaceState(null, '', '/login?code=c1&state=s1')
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: false, oidc: [entraProvider] },
      isLoading: false,
    })
    jest
      .mocked(sessionStorage.getItem)
      .mockImplementation((key) =>
        key === 'wayd.oidc.chosenProvider' ? entraProvider.name : null,
      )
    jest.mocked(UserManager).mockImplementationOnce(
      () =>
        ({
          signinRedirectCallback: jest
            .fn()
            .mockResolvedValue({ access_token: 'entra-token' }),
        }) as never,
    )
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

describe('LoginPage rate-limited sign-in', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('tells the user how long to wait, rather than reporting a server fault', async () => {
    // Arrange — the shape the generated client throws for a 429 the endpoint does not document
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: true, oidc: [] },
      isLoading: false,
    })
    const refused = new ApiException(
      'An unexpected server error occurred.',
      429,
      JSON.stringify({
        status: 429,
        title: 'Too Many Requests',
        detail: 'Too many requests. Try again in 42 seconds.',
      }),
      { 'retry-after': '42' },
      null,
    )
    jest.mocked(getAuthClient).mockReturnValue({
      login: jest.fn().mockRejectedValue(refused),
    } as never)
    render(<LoginPage />)

    // Act
    fireEvent.change(screen.getByPlaceholderText('Email'), {
      target: { value: 'pat@acme.example' },
    })
    fireEvent.change(screen.getByPlaceholderText('Password'), {
      target: { value: 'correct horse' },
    })
    // In act: jest.setup stubs MessageChannel, so React commits the handler's
    // state updates, made after the awaited sign-in, only inside act.
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    })

    // Assert
    expect(
      await screen.findByText('Too many requests. Try again in 42 seconds.'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('An unexpected server error occurred.'),
    ).not.toBeInTheDocument()
  })
})

describe('LoginPage rate-limited identity-provider sign-in', () => {
  afterEach(() => {
    window.history.replaceState(null, '', '/')
    jest.mocked(sessionStorage.getItem).mockReset()
  })

  it('tells the user how long to wait when the exchange is refused', async () => {
    // Arrange — back from the provider, with the exchange refused by its limit
    jest.clearAllMocks()
    window.history.replaceState(null, '', '/login?code=c1&state=s1')
    mockUseGetAuthProvidersQuery.mockReturnValue({
      data: { local: false, oidc: [entraProvider] },
      isLoading: false,
    })
    jest
      .mocked(sessionStorage.getItem)
      .mockImplementation((key) =>
        key === 'wayd.oidc.chosenProvider' ? entraProvider.name : null,
      )
    jest.mocked(UserManager).mockImplementationOnce(
      () =>
        ({
          signinRedirectCallback: jest
            .fn()
            .mockResolvedValue({ access_token: 'entra-token' }),
        }) as never,
    )
    const refused = new ApiException(
      'An unexpected server error occurred.',
      429,
      JSON.stringify({
        status: 429,
        detail: 'Too many requests. Try again in 17 seconds.',
      }),
      { 'retry-after': '17' },
      null,
    )
    jest.mocked(getAuthClient).mockReturnValue({
      exchange: jest.fn().mockRejectedValue(refused),
    } as never)

    // Act — in act, for the reason given on the password sign-in test
    await act(async () => {
      render(<LoginPage />)
      await new Promise((resolve) => setTimeout(resolve, 0))
    })

    // Assert
    expect(
      await screen.findByText('Too many requests. Try again in 17 seconds.'),
    ).toBeInTheDocument()
    expect(storeAuth).not.toHaveBeenCalled()
  })
})
