import { render, screen, waitFor, act } from '@testing-library/react'
import '@testing-library/jest-dom'
import React from 'react'

// --- Mocks (all before imports) ---

jest.mock('../../../app/account/profile/change-password-form', () => ({
  __esModule: true,
  default: ({ onFormComplete, onFormCancel }: any) => (
    <div data-testid="change-password-form">
      <button onClick={onFormComplete} data-testid="change-password-complete">
        Complete
      </button>
      <button onClick={onFormCancel} data-testid="change-password-cancel">
        Cancel
      </button>
    </div>
  ),
}))

// oidc-client-registry mock — logout path may call signoutRedirect; the
// session-reading behavior we're actually testing doesn't touch it.
jest.mock('./oidc-client-registry', () => ({
  signoutRedirect: jest.fn().mockResolvedValue(undefined),
}))

// auth-providers-api mock — AuthProvider queries this to resolve the OIDC
// provider for logout. Not relevant to the session-reading tests.
jest.mock('../../../store/features/common/auth-providers-api', () => ({
  useGetAuthProvidersQuery: () => ({ data: undefined }),
}))

jest.mock('./../../contexts/theme/use-theme', () => ({
  __esModule: true,
  default: () => ({
    token: { colorBgContainer: '#ffffff' },
    currentTheme: 'wayd',
    currentMode: 'light',
  }),
}))

// In-memory storage for the Wayd JWT. We replace the real services/clients
// module entirely because clients.ts imports axios + NSwag-generated code,
// which pulls in too much for a unit test and triggers side effects on module
// load. The real behavior under test is AuthProvider's session-reading logic.
const mockStorage = {
  token: null as string | null,
  refreshToken: null as string | null,
  tokenExpiry: null as string | null,
  mustChangePassword: null as string | null,
}

const mockLogin = jest.fn()
const mockLogout = jest.fn()

jest.mock('@/src/services/clients', () => ({
  AUTH_TOKEN_KEY: 'wayd.token',
  AUTH_REFRESH_TOKEN_KEY: 'wayd.refreshToken',
  AUTH_TOKEN_EXPIRY_KEY: 'wayd.tokenExpiry',
  AUTH_MUST_CHANGE_PASSWORD_KEY: 'wayd.mustChangePassword',
  getAuthStorage: () => ({
    getItem: (key: string) => {
      if (key === 'wayd.mustChangePassword') return mockStorage.mustChangePassword
      if (key === 'wayd.token') return mockStorage.token
      return null
    },
    setItem: (key: string, value: string) => {
      if (key === 'wayd.mustChangePassword') mockStorage.mustChangePassword = value
    },
    removeItem: (key: string) => {
      if (key === 'wayd.mustChangePassword') mockStorage.mustChangePassword = null
    },
  }),
  getAuthToken: () => mockStorage.token,
  getAuthRefreshToken: () => mockStorage.refreshToken,
  isAuthActive: () => mockStorage.token !== null,
  storeAuth: (tokenResponse: any) => {
    mockStorage.token = tokenResponse.token
    mockStorage.refreshToken = tokenResponse.refreshToken
    mockStorage.tokenExpiry = new Date(tokenResponse.tokenExpiresAt).toISOString()
    mockStorage.mustChangePassword = tokenResponse.mustChangePassword ? 'true' : null
  },
  clearAuth: () => {
    mockStorage.token = null
    mockStorage.refreshToken = null
    mockStorage.tokenExpiry = null
    mockStorage.mustChangePassword = null
  },
  getAuthClient: () => ({
    login: mockLogin,
  }),
  getAuthenticatedAuthClient: () => ({
    logout: mockLogout,
  }),
}))

// Imports after mocks.
import { AuthProvider } from './auth-context'
import useAuth from './use-auth'
import {
  clearAuth,
  getAuthRefreshToken,
  storeAuth,
} from '@/src/services/clients'

// --- Test helpers ---

// JWT segments are base64url, not standard base64 — convert btoa's output so
// tests exercise the same decode path real Wayd tokens hit (`-`/`_` instead of
// `+`/`/`, stripped padding).
function base64UrlEncode(value: string): string {
  return btoa(value).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function makeWaydJwt(claims: Record<string, unknown>): string {
  const header = base64UrlEncode(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = base64UrlEncode(JSON.stringify(claims))
  return `${header}.${payload}.signature`
}

const TestConsumer = () => {
  const auth = useAuth()
  return (
    <div>
      <div data-testid="name">{auth.user?.name ?? ''}</div>
      <div data-testid="username">{auth.user?.username ?? ''}</div>
      <div data-testid="authenticated">
        {auth.user?.isAuthenticated ? 'yes' : 'no'}
      </div>
      <div data-testid="auth-method">{auth.authMethod ?? 'null'}</div>
      <div data-testid="employee-id">{auth.user?.employeeId ?? ''}</div>
      <div data-testid="permissions">
        {auth.user?.claims
          .filter((c) => c.type === 'Permission')
          .map((c) => c.value)
          .join(',') ?? ''}
      </div>
      <div data-testid="has-projects-view">
        {auth.hasPermissionClaim('Permissions.Projects.View') ? 'yes' : 'no'}
      </div>
      <div data-testid="must-change-password">
        {auth.mustChangePassword ? 'yes' : 'no'}
      </div>
    </div>
  )
}

beforeEach(() => {
  jest.clearAllMocks()
  clearAuth()
})

afterEach(() => {
  clearAuth()
})

// --- Tests ---

describe('AuthProvider hydration from stored Wayd JWT', () => {
  it('hydrates user, permissions, and authMethod from a Wayd-provider JWT', async () => {
    storeAuth({
      token: makeWaydJwt({
        given_name: 'Alice',
        family_name: 'Example',
        email: 'alice@example.com',
        loginProvider: 'Wayd',
        EmployeeId: 'emp-123',
        permission: ['Permissions.Projects.View', 'Permissions.Teams.Manage'],
      }),
      refreshToken: 'r1',
      tokenExpiresAt: new Date(Date.now() + 3600_000),
      mustChangePassword: false,
    })

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    await waitFor(() => {
      expect(screen.getByTestId('authenticated')).toHaveTextContent('yes')
    })
    expect(screen.getByTestId('name')).toHaveTextContent('Alice Example')
    expect(screen.getByTestId('username')).toHaveTextContent('alice@example.com')
    expect(screen.getByTestId('auth-method')).toHaveTextContent('local')
    expect(screen.getByTestId('employee-id')).toHaveTextContent('emp-123')
    expect(screen.getByTestId('permissions')).toHaveTextContent(
      'Permissions.Projects.View,Permissions.Teams.Manage',
    )
    expect(screen.getByTestId('has-projects-view')).toHaveTextContent('yes')
  })

  it('hydrates an OIDC-issued JWT with authMethod = oidc', async () => {
    storeAuth({
      token: makeWaydJwt({
        given_name: 'Bob',
        family_name: 'Cloud',
        email: 'bob@acme.com',
        loginProvider: 'MicrosoftEntraId',
        permission: [],
      }),
      refreshToken: 'r2',
      tokenExpiresAt: new Date(Date.now() + 3600_000),
      mustChangePassword: false,
    })

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(screen.getByTestId('authenticated')).toHaveTextContent('yes')
    expect(screen.getByTestId('auth-method')).toHaveTextContent('oidc')
  })

  it('supports permission claim as a single string (not just array)', () => {
    storeAuth({
      token: makeWaydJwt({
        given_name: 'Solo',
        loginProvider: 'Wayd',
        permission: 'Permissions.Projects.View',
      }),
      refreshToken: 'r',
      tokenExpiresAt: new Date(Date.now() + 3600_000),
      mustChangePassword: false,
    })

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(screen.getByTestId('has-projects-view')).toHaveTextContent('yes')
  })

  it('clears storage when the stored JWT is malformed', () => {
    mockStorage.token = 'not-a-valid-jwt'

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(screen.getByTestId('authenticated')).toHaveTextContent('no')
    // Malformed tokens are purged so the user falls through to login.
    expect(mockStorage.token).toBeNull()
  })

  it('shows change-password gate when mustChangePassword is set on a local session', () => {
    storeAuth({
      token: makeWaydJwt({
        given_name: 'Dana',
        loginProvider: 'Wayd',
        permission: [],
      }),
      refreshToken: 'r',
      tokenExpiresAt: new Date(Date.now() + 3600_000),
      mustChangePassword: true,
    })

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(screen.getByTestId('change-password-form')).toBeInTheDocument()
  })
})

describe('AuthProvider local login', () => {
  const TestLoginConsumer = () => {
    const auth = useAuth()
    return (
      <div>
        <div data-testid="authenticated">
          {auth.user?.isAuthenticated ? 'yes' : 'no'}
        </div>
        <button
          data-testid="do-login"
          onClick={() => auth.localLogin('alice', 'password')}
        >
          login
        </button>
      </div>
    )
  }

  it('stores the token response and hydrates the user on success', async () => {
    mockLogin.mockResolvedValue({
      token: makeWaydJwt({
        given_name: 'Alice',
        loginProvider: 'Wayd',
        permission: [],
      }),
      refreshToken: 'r',
      tokenExpiresAt: new Date(Date.now() + 3600_000),
      mustChangePassword: false,
    })

    render(
      <AuthProvider>
        <TestLoginConsumer />
      </AuthProvider>,
    )

    expect(screen.getByTestId('authenticated')).toHaveTextContent('no')

    await act(async () => {
      screen.getByTestId('do-login').click()
    })

    expect(screen.getByTestId('authenticated')).toHaveTextContent('yes')
    expect(mockLogin).toHaveBeenCalledWith({
      userName: 'alice',
      password: 'password',
    })
  })
})
describe('logout', () => {
  const TestLogoutConsumer = () => {
    const auth = useAuth()
    return (
      <div>
        <div data-testid="authenticated">
          {auth.user?.isAuthenticated ? 'yes' : 'no'}
        </div>
        <button data-testid="do-logout" onClick={() => auth.logout()}>
          logout
        </button>
      </div>
    )
  }

  function signIn() {
    storeAuth({
      token: makeWaydJwt({
        given_name: 'Alice',
        loginProvider: 'Wayd',
        permission: [],
      }),
      refreshToken: 'refresh-token-value',
      tokenExpiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      mustChangePassword: false,
    } as any)
  }

  it('revokes the session server-side before clearing local storage', async () => {
    // The call must happen while the bearer token still exists; clearAuth() removes it.
    let refreshTokenAtCallTime: string | null = null
    mockLogout.mockImplementation(async () => {
      refreshTokenAtCallTime = getAuthRefreshToken()
    })
    signIn()

    render(
      <AuthProvider>
        <TestLogoutConsumer />
      </AuthProvider>,
    )

    await act(async () => {
      screen.getByTestId('do-logout').click()
    })

    expect(mockLogout).toHaveBeenCalledTimes(1)
    expect(refreshTokenAtCallTime).toBe('refresh-token-value')
    expect(getAuthRefreshToken()).toBeNull()
  })

  it('still signs out locally when the revoke call fails', async () => {
    // A network error must not strand the user in a signed-in UI.
    mockLogout.mockRejectedValue(new Error('Network Error'))
    signIn()

    render(
      <AuthProvider>
        <TestLogoutConsumer />
      </AuthProvider>,
    )

    await act(async () => {
      screen.getByTestId('do-logout').click()
    })

    expect(mockLogout).toHaveBeenCalledTimes(1)
    expect(getAuthRefreshToken()).toBeNull()
  })
})
