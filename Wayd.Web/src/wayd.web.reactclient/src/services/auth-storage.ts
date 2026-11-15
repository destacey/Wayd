// Storage keys for the Wayd JWT session. The same keys are used for every
// login provider — local JWT and Entra-exchanged sessions are indistinguishable
// at the storage layer.
export const AUTH_TOKEN_KEY = 'wayd.local.token'
export const AUTH_REFRESH_TOKEN_KEY = 'wayd.local.refreshToken'
export const AUTH_TOKEN_EXPIRY_KEY = 'wayd.local.tokenExpiry'
export const AUTH_MUST_CHANGE_PASSWORD_KEY = 'wayd.local.mustChangePassword'
const AUTH_REMEMBER_KEY = 'wayd.local.rememberMe'

/**
 * Returns the storage backend for the auth session.
 * The routing flag in localStorage determines whether tokens live in
 * localStorage (remember me) or sessionStorage (ephemeral session).
 */
export function getAuthStorage(): Storage {
  if (typeof window === 'undefined') {
    throw new Error('getAuthStorage() cannot be called during SSR')
  }
  return localStorage.getItem(AUTH_REMEMBER_KEY) === 'false'
    ? sessionStorage
    : localStorage
}

/** Sets the remember-me routing flag (always in localStorage). */
export function setRememberMe(remember: boolean): void {
  localStorage.setItem(AUTH_REMEMBER_KEY, remember ? 'true' : 'false')
}

export function getAuthToken(): string | null {
  if (typeof window === 'undefined') return null
  return getAuthStorage().getItem(AUTH_TOKEN_KEY)
}

export function getAuthRefreshToken(): string | null {
  if (typeof window === 'undefined') return null
  return getAuthStorage().getItem(AUTH_REFRESH_TOKEN_KEY)
}

/**
 * Returns true when the stored token is expired or within the skew window of
 * expiring. Used by the request interceptor to refresh proactively rather than
 * letting every concurrent call 401 first. The skew also covers small clock
 * drift between client and server.
 *
 * Returns false (treat as fresh) when there's no expiry recorded — older
 * sessions that predate the expiry-stamp landing should fall through to the
 * reactive 401 path instead of being force-refreshed on every request.
 */
const TOKEN_REFRESH_SKEW_MS = 30_000
export function isAuthTokenExpiringSoon(): boolean {
  if (typeof window === 'undefined') return false
  const stamp = getAuthStorage().getItem(AUTH_TOKEN_EXPIRY_KEY)
  if (!stamp) return false
  const expiresAt = Date.parse(stamp)
  if (Number.isNaN(expiresAt)) return false
  return expiresAt - Date.now() <= TOKEN_REFRESH_SKEW_MS
}

export function isAuthActive(): boolean {
  if (typeof window === 'undefined') return false
  // Check both storages since we don't know which was used until we read the flag.
  return !!(
    localStorage.getItem(AUTH_TOKEN_KEY) ||
    sessionStorage.getItem(AUTH_TOKEN_KEY)
  )
}

export function clearAuth(): void {
  // Clear from both storages to ensure clean state.
  for (const storage of [localStorage, sessionStorage]) {
    storage.removeItem(AUTH_TOKEN_KEY)
    storage.removeItem(AUTH_REFRESH_TOKEN_KEY)
    storage.removeItem(AUTH_TOKEN_EXPIRY_KEY)
    storage.removeItem(AUTH_MUST_CHANGE_PASSWORD_KEY)
    // oidc-client-ts's redirect state, such as the PKCE verifier of a sign-in
    // that was started and never completed.
    for (const key of Object.keys(storage)) {
      if (key.startsWith('oidc.')) storage.removeItem(key)
    }
  }
  localStorage.removeItem(AUTH_REMEMBER_KEY)
}

/** Persists a token response from /api/auth/login, /exchange, or /refresh-token. */
export function storeAuth(tokenResponse: {
  token: string
  refreshToken: string
  tokenExpiresAt: Date | string
  mustChangePassword: boolean
}): void {
  const storage = getAuthStorage()
  storage.setItem(AUTH_TOKEN_KEY, tokenResponse.token)
  storage.setItem(AUTH_REFRESH_TOKEN_KEY, tokenResponse.refreshToken)
  storage.setItem(
    AUTH_TOKEN_EXPIRY_KEY,
    new Date(tokenResponse.tokenExpiresAt).toISOString(),
  )
  if (tokenResponse.mustChangePassword) {
    storage.setItem(AUTH_MUST_CHANGE_PASSWORD_KEY, 'true')
  } else {
    storage.removeItem(AUTH_MUST_CHANGE_PASSWORD_KEY)
  }
}
