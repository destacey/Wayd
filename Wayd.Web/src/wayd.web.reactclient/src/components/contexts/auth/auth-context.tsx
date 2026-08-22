'use client'

import React, { createContext, useCallback, useMemo, useState } from 'react'
import { AuthContextType, AuthMethod, Claim, User } from './types'
import ChangePasswordForm from '@/src/components/common/forms/change-password-form'
import styles from './auth-provider.module.css'
import {
  AUTH_MUST_CHANGE_PASSWORD_KEY,
  clearAuth,
  getAuthClient,
  getAuthenticatedAuthClient,
  getAuthRefreshToken,
  getAuthStorage,
  getAuthToken,
  isAuthActive,
  storeAuth,
} from '@/src/services/clients'
import { signoutRedirect } from './oidc-client-registry'
import { useGetAuthProvidersQuery } from '@/src/store/features/common/auth-providers-api'

export const AuthContext = createContext<AuthContextType | null>(null)

// --- JWT decoding ---

type DecodedClaims = {
  name: string
  username: string
  employeeId: string | null
  loginProvider: string | null
  permissions: string[]
  genericClaims: Claim[]
}

const PERMISSION_CLAIM = 'permission'
const EMPLOYEE_ID_CLAIM = 'EmployeeId'
const LOGIN_PROVIDER_CLAIMS = [
  'loginProvider',
  'http://schemas.microsoft.com/identity/claims/loginprovider',
]
const GIVEN_NAME_CLAIMS = [
  'given_name',
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
  'name',
]
const SURNAME_CLAIMS = [
  'family_name',
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname',
]
const USERNAME_CLAIMS = [
  'email',
  'preferred_username',
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress',
]

// Claims we surface as dedicated fields on User (or as a separate permissions
// set). We skip them in the generic sweep so the Claims tab doesn't show them
// twice — once in the sweep and once in the explicit promotion.
const PROMOTED_CLAIM_KEYS = new Set<string>([
  PERMISSION_CLAIM,
  EMPLOYEE_ID_CLAIM,
  ...LOGIN_PROVIDER_CLAIMS,
  ...GIVEN_NAME_CLAIMS,
  ...SURNAME_CLAIMS,
  ...USERNAME_CLAIMS,
])

function firstDefined(
  payload: Record<string, unknown>,
  keys: readonly string[],
): string | null {
  for (const key of keys) {
    const value = payload[key]
    if (typeof value === 'string' && value.length > 0) return value
  }
  return null
}

// RFC 7515 §2: JWT segments are base64url-encoded (URL-safe alphabet, padding
// stripped). atob only speaks standard base64, so any segment containing `-`
// or `_`, or one whose length isn't a multiple of 4, fails to decode. Convert
// to standard base64 before handing it to atob.
function base64UrlDecode(input: string): string {
  const normalized = input.replace(/-/g, '+').replace(/_/g, '/')
  const padded = normalized.padEnd(
    normalized.length + ((4 - (normalized.length % 4)) % 4),
    '=',
  )
  return atob(padded)
}

function decodeWaydJwt(token: string): DecodedClaims | null {
  try {
    const payloadPart = token.split('.')[1]
    if (!payloadPart) return null
    const payload = JSON.parse(base64UrlDecode(payloadPart)) as Record<
      string,
      unknown
    >


    const rawPermissions = payload[PERMISSION_CLAIM]
    const permissions = Array.isArray(rawPermissions)
      ? rawPermissions.filter((p): p is string => typeof p === 'string')
      : typeof rawPermissions === 'string'
        ? [rawPermissions]
        : []

    const given = firstDefined(payload, GIVEN_NAME_CLAIMS) ?? ''
    const surname = firstDefined(payload, SURNAME_CLAIMS) ?? ''
    const name = [given, surname].filter(Boolean).join(' ')
    const username = firstDefined(payload, USERNAME_CLAIMS) ?? ''
    const employeeId =
      typeof payload[EMPLOYEE_ID_CLAIM] === 'string'
        ? (payload[EMPLOYEE_ID_CLAIM] as string)
        : null
    const loginProvider = firstDefined(payload, LOGIN_PROVIDER_CLAIMS)

    const genericClaims: Claim[] = []
    for (const [key, value] of Object.entries(payload)) {
      if (PROMOTED_CLAIM_KEYS.has(key)) continue
      if (typeof value === 'string') {
        genericClaims.push({ type: key, value })
      }
    }

    return {
      name,
      username,
      employeeId,
      loginProvider,
      permissions,
      genericClaims,
    }
  } catch {
    return null
  }
}

function authMethodFromLoginProvider(
  loginProvider: string | null,
): AuthMethod {
  if (loginProvider === 'Wayd') return 'local'
  if (loginProvider) return 'oidc'
  return null
}

// --- Session derivation ---

const UNAUTHENTICATED_USER: User = {
  name: '',
  username: '',
  isAuthenticated: false,
  employeeId: null,
  claims: [],
}

type SessionSnapshot = {
  user: User
  permissionsSet: Set<string>
  authMethod: AuthMethod
  loginProvider: string | null
  mustChangePassword: boolean
}

const EMPTY_SESSION: SessionSnapshot = {
  user: UNAUTHENTICATED_USER,
  permissionsSet: new Set(),
  authMethod: null,
  loginProvider: null,
  mustChangePassword: false,
}

/**
 * Pure function: reads whatever is in storage and returns the session snapshot.
 * Used for the initial render (lazy useState initializer) and for cross-tab
 * storage events. No side effects except clearing a malformed token.
 */
function deriveSessionFromStorage(): SessionSnapshot {
  if (typeof window === 'undefined') return EMPTY_SESSION

  const storedToken = getAuthToken()
  if (!storedToken) return EMPTY_SESSION

  const decoded = decodeWaydJwt(storedToken)
  if (!decoded) {
    // Malformed token — purge it so the app falls through to /login cleanly.
    clearAuth()
    return EMPTY_SESSION
  }

  const claims: Claim[] = [...decoded.genericClaims]
  if (decoded.employeeId) {
    claims.push({ type: EMPLOYEE_ID_CLAIM, value: decoded.employeeId })
  }
  for (const permission of decoded.permissions) {
    claims.push({ type: 'Permission', value: permission })
  }

  return {
    user: {
      name: decoded.name,
      username: decoded.username,
      isAuthenticated: true,
      employeeId: decoded.employeeId,
      claims,
    },
    permissionsSet: new Set(decoded.permissions),
    authMethod: authMethodFromLoginProvider(decoded.loginProvider),
    loginProvider: decoded.loginProvider,
    mustChangePassword:
      getAuthStorage().getItem(AUTH_MUST_CHANGE_PASSWORD_KEY) === 'true',
  }
}

/**
 * AuthProvider — reads the current session from the stored Wayd JWT and
 * exposes it to the app. That's all it does.
 *
 * No exchange logic, no MSAL watching, no auto-redirects. Getting a JWT
 * into storage is the login page's job; removing it is the logout action's
 * job. AuthProvider just decodes whatever is there.
 *
 * The login page / MSAL redirect handler is responsible for:
 *   - Initiating Entra or local login.
 *   - Running /api/auth/exchange on the MSAL redirect response.
 *   - Storing the Wayd JWT.
 *   - Navigating to the protected app.
 *
 * This separation means AuthProvider doesn't need to distinguish "just logged
 * in, exchange pending" from "just logged out, MSAL cache still present" —
 * a distinction it can't actually make, and the source of the logout race
 * we hit during PR 3.2 testing.
 */
export const AuthProvider = ({ children }: { children: React.ReactNode }) => {
  const { data: providers } = useGetAuthProvidersQuery()

  const [session, setSession] = useState<SessionSnapshot>(
    deriveSessionFromStorage,
  )

  const refresh = useCallback(() => {
    setSession(deriveSessionFromStorage())
  }, [])

  // Cross-tab sync. If another tab logs in, logs out, or refreshes the token,
  // our stored session might be stale — rehydrate.
  React.useEffect(() => {
    const handleStorageChange = (event: StorageEvent) => {
      if (event.key !== null && !event.key?.startsWith('wayd.local.')) return
      refresh()
    }
    window.addEventListener('storage', handleStorageChange, { passive: true })
    return () => window.removeEventListener('storage', handleStorageChange)
  }, [refresh])

  // --- Actions ---

  const acquireToken = useCallback(async (): Promise<string> => {
    const t = getAuthToken()
    if (t) return t
    throw new Error('No auth token found')
  }, [])

  const refreshUser = useCallback(async () => {
    refresh()
  }, [refresh])

  // login() is intentionally a no-op stub here. The login page owns the
  // actual MSAL loginRedirect call — AuthProvider doesn't need to know
  // about MSAL at all. Kept in the context to satisfy the interface while
  // consumers (e.g., 401 recovery paths) are migrated to route-based navigation.
  const login = useCallback(async () => {
    // eslint-disable-next-line react-compiler/react-compiler
    window.location.href = '/login'
  }, [])

  const localLogin = useCallback(
    async (username: string, password: string) => {
      const tokenResponse = await getAuthClient().login({
        userName: username,
        password,
      })
      storeAuth(tokenResponse)
      refresh()
    },
    [refresh],
  )

  const logout = useCallback(async () => {
    // Must precede clearAuth(), which removes both the bearer token this call
    // authenticates with and the refresh token that identifies which session to end.
    // Failure is swallowed deliberately: stranding someone in a signed-in UI is worse
    // than a session outliving the click.
    try {
      await getAuthenticatedAuthClient().logout({
        refreshToken: getAuthRefreshToken() ?? undefined,
      })
    } catch {
      // Ignored — local sign-out proceeds regardless.
    }

    clearAuth()
    const loginProvider = session.loginProvider
    if (loginProvider && session.authMethod === 'oidc') {
      // Find the matching provider config so signoutRedirect can call the
      // correct end_session_endpoint. Falls back to /login if not found.
      const providerInfo = providers?.oidc.find((p) => p.name === loginProvider)
      if (providerInfo) {
        await signoutRedirect(providerInfo)
        return
      }
    }
    window.location.href = '/login'
  }, [session.loginProvider, session.authMethod, providers])

  // --- Context value ---

  const authContext: AuthContextType = useMemo(
    () => ({
      user: session.user,
      isLoading: false,
      authMethod: session.authMethod,
      mustChangePassword: session.mustChangePassword,
      acquireToken,
      refreshUser,
      hasClaim: (type: string, value: string) =>
        session.user.claims.some((c) => c.type === type && c.value === value),
      hasPermissionClaim: (value: string) =>
        session.permissionsSet.has(value),
      login,
      localLogin,
      logout,
    }),
    [session, acquireToken, refreshUser, login, localLogin, logout],
  )

  // --- Forced password change gate ---

  if (
    session.mustChangePassword &&
    session.authMethod === 'local' &&
    session.user.isAuthenticated
  ) {
    return (
      <AuthContext.Provider value={authContext}>
        <div className={styles.changePasswordBackground}>
          <ChangePasswordForm
            required
            onFormComplete={() => {
              // Form triggers logout on completion.
            }}
            onFormCancel={() => logout()}
          />
        </div>
      </AuthContext.Provider>
    )
  }

  return (
    <AuthContext.Provider value={authContext}>{children}</AuthContext.Provider>
  )
}

export default AuthProvider

/** @internal exported for tests */
export const _internal = {
  decodeWaydJwt,
  deriveSessionFromStorage,
  authMethodFromLoginProvider,
  isAuthActive,
}
