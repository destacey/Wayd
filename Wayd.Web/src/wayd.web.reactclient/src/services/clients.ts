import axios from 'axios'
import {
  AuthClient,
  BackgroundJobsClient,
  EmployeesClient,
  HealthChecksClient,
  LinksClient,
  OidcProvidersClient,
  PermissionsClient,
  ProfileClient,
  PlanningIntervalsClient,
  RisksClient,
  RolesClient,
  TeamsClient,
  TeamMemberRolesClient,
  TeamsOfTeamsClient,
  UsersClient,
  WorkStatusesClient,
  WorkTypesClient,
  WorkProcessesClient,
  WorkspacesClient,
  WorkTypeLevelsClient,
  WorkTypeTiersClient,
  RoadmapsClient,
  StrategicThemesClient,
  PortfoliosClient,
  ProgramsClient,
  ExpenditureCategoriesClient,
  ProjectsClient,
  ProjectHealthChecksClient,
  ProjectScoresClient,
  ProjectTasksClient,
  StrategicInitiativesClient,
  ProjectLifecyclesClient,
  ScoringModelsClient,
  SprintsClient,
  EstimationScalesClient,
  PokerSessionsClient,
  ConnectionsClient,
  AzureDevOpsConnectionsClient,
  FeatureFlagsClient,
  MessagingClient,
  PersonalAccessTokensClient,
  SearchClient,
  TokenResponse,
} from './wayd-api'

const apiUrl = process.env.NEXT_PUBLIC_API_BASE_URL

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

// Unauthenticated axios client for login/refresh/exchange (no token interceptors).
const unauthAxiosClient = axios.create({
  baseURL: apiUrl,
  timeout: 30000,
})

// Auth client uses unauthenticated axios — login, refresh, exchange, and
// getProviders are all [AllowAnonymous] on the backend.
export const getAuthClient = () => new AuthClient('', unauthAxiosClient)

const axiosClient = axios.create({
  baseURL: apiUrl,
  timeout: 30000, // 30 second timeout
})

/**
 * True for axios failures where no response was received: offline, DNS failure,
 * server unreachable, request timed out, request aborted. We treat these as
 * "couldn't ask the server" rather than "server said no" — the distinction
 * matters when deciding whether a failed refresh proves the session is bad.
 *
 * Axios sets `response` only when the server actually responded; for transport
 * failures it sets `code` to ERR_NETWORK / ECONNABORTED / ERR_CANCELED. We
 * accept both signals because some axios errors get reshaped by interceptors
 * before reaching us.
 */
function isNetworkError(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false
  const e = error as { response?: unknown; code?: string; message?: string }
  if (e.response) return false
  if (e.code === 'ERR_NETWORK' || e.code === 'ECONNABORTED' || e.code === 'ERR_CANCELED') {
    return true
  }
  return e.message === 'Network Error'
}

function redirectToLoginWithReturnUrl(): void {
  if (typeof window === 'undefined') return
  const { pathname } = window.location
  if (pathname && pathname !== '/' && pathname !== '/login' && pathname !== '/logout') {
    sessionStorage.setItem('wayd.returnUrl', pathname)
  }
  window.location.href = '/login'
}

// Single-flight guard for refresh. When multiple requests 401 concurrently
// (typical after an idle period: every in-flight call fails at once), they all
// share one refresh call instead of each minting a new refresh token and racing
// the rotation. Without this, whichever response wrote to storage last wins and
// the others' rotated refresh tokens are orphaned — which breaks as soon as the
// backend adopts one-shot refresh semantics.
//
// Returns null when there are no tokens to refresh with. Callers distinguish
// this from a refresh-attempt failure: no tokens means "not a session" (don't
// wipe state, just reject), whereas a failure means "session invalidated by
// the server" (clear it).
let inFlightRefresh: Promise<TokenResponse> | null = null

function refreshOnce(): Promise<TokenResponse> | null {
  if (inFlightRefresh) return inFlightRefresh

  const currentToken = getAuthToken()
  const refreshToken = getAuthRefreshToken()
  if (!currentToken || !refreshToken) return null

  inFlightRefresh = getAuthClient()
    .refreshToken({ token: currentToken, refreshToken })
    .then((tokenResponse) => {
      storeAuth(tokenResponse)
      return tokenResponse
    })
    .finally(() => {
      inFlightRefresh = null
    })

  return inFlightRefresh
}

/**
 * Returns the current Wayd JWT, proactively refreshing first if it's expired or
 * within the refresh skew window. Shared by the axios request interceptor and
 * the SignalR accessTokenFactory so both paths use the same single-flight
 * refresh (inFlightRefresh) — important because a hub start/reconnect happens
 * outside the axios pipeline and would otherwise hand SignalR a stale token,
 * causing it to fall back to polling until the hook remounts.
 *
 * Refresh failure is non-fatal here: we still return whatever token is in
 * storage so the caller can attempt the request. The 401-recovery path in the
 * response interceptor handles cleanup if the stale token is rejected; for
 * SignalR, the hub's automatic-reconnect will try again once a fresh token
 * lands. Network failures inside refreshOnce() also fall through to send-as-is
 * — better to let the actual call decide than to fail eagerly here.
 */
export async function getFreshAuthToken(): Promise<string | null> {
  if (isAuthTokenExpiringSoon()) {
    const refresh = refreshOnce()
    if (refresh) {
      try {
        await refresh
      } catch {
        // Swallowed deliberately — see comment above.
      }
    }
  }
  return getAuthToken()
}

// Response interceptor: automatic Wayd refresh-token retry on 401.
// One path for every provider because every provider ends up with the same
// Wayd JWT shape in storage after login/exchange. No MSAL involvement — the
// Wayd refresh token is the sole mechanism for extending the session.
axiosClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    if (
      error.response?.status === 401 &&
      originalRequest &&
      !(originalRequest as any)._retry
    ) {
      ;(originalRequest as any)._retry = true

      const refresh = refreshOnce()
      if (refresh) {
        try {
          const tokenResponse = await refresh
          originalRequest.headers = originalRequest.headers || {}
          originalRequest.headers.Authorization = `Bearer ${tokenResponse.token}`
          return axiosClient(originalRequest)
        } catch (refreshError) {
          // The refresh attempt itself failed. Two distinct cases:
          //
          // 1. Server reachable, refresh rejected (4xx/5xx): the session is
          //    invalid. Wipe storage and hard-navigate to /login so AuthGate
          //    re-renders unauthenticated. We also stash the current path
          //    under the same key AuthGate uses (see app/layout.tsx) so the
          //    user lands back where they were.
          //
          // 2. Network error (offline, server unreachable, request aborted):
          //    no proof the session is bad — we just couldn't ask. Wiping
          //    storage here would log out a user every time their wifi
          //    blipped. Leave storage intact and reject the caller with the
          //    original 401, not the refresh-call error — the caller asked
          //    about their endpoint, not /refresh-token, so surfacing the
          //    refresh URL/message would be misleading. A future request can
          //    re-attempt the refresh once the network recovers.
          if (isNetworkError(refreshError)) {
            console.warn(
              'Token refresh on 401 unreachable; leaving session intact:',
              refreshError,
            )
            return Promise.reject(error)
          }

          console.error('Token refresh on 401 failed:', refreshError)
          clearAuth()
          redirectToLoginWithReturnUrl()
          // Reject early so the generic "API Error" log + downstream error
          // handlers don't run for a 401 we've already handled.
          return Promise.reject(refreshError)
        }
      }
      // No tokens to refresh with: the caller was already unauthenticated.
      // Redirect to /login and preserve return URL, but do not clear auth
      // storage so we don't clobber the remember-me preference.
      redirectToLoginWithReturnUrl()
      return Promise.reject(error)
    }

    // Quiet log for transient network errors — RTK Query retries plus
    // refetch-on-focus mean a single offline blip can produce dozens of
    // identical entries, and each endpoint's own queryFn already logs the
    // error with its endpoint context. Real server errors stay loud.
    if (isNetworkError(error)) {
      console.warn('API unreachable:', error.config?.url)
    } else {
      console.error('API Error:', error.message, error.config?.url)
    }
    return Promise.reject(error)
  },
)

// Request interceptor: attach the Wayd JWT to outgoing requests, refreshing
// proactively when the stored token is expired or about to expire.
//
// Why proactive: when a user returns after a long idle, the menu page fires a
// dozen queries in parallel. Without this, each one round-trips to the API,
// 401s, and queues a refresh attempt — fast on a healthy network, but on a
// flaky one each doomed request burns time and floods the console with errors
// before the response interceptor even gets a chance to recover. Refreshing
// here means the failure surface is the single /refresh-token call, not N.
//
// Refresh failure here doesn't reject the request; we send it with the stale
// token and let the response interceptor's 401 path handle storage cleanup
// and the redirect to /login. Single flow for "session is bad," kept in one
// place. Network failures inside refreshOnce() also fall through to send-as-is
// — better to let the actual call decide than to fail eagerly here.
axiosClient.interceptors.request.use(
  async (config) => {
    const token = await getFreshAuthToken()
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error),
)

// API Clients
export const getConnectionsClient = () => new ConnectionsClient('', axiosClient)

export const getAzureDevOpsConnectionsClient = () =>
  new AzureDevOpsConnectionsClient('', axiosClient)

export const getBackgroundJobsClient = () =>
  new BackgroundJobsClient('', axiosClient)

export const getMessagingClient = () => new MessagingClient('', axiosClient)

export const getHealthChecksClient = () =>
  new HealthChecksClient('', axiosClient)

export const getLinksClient = () => new LinksClient('', axiosClient)

// ORGANIZATION
export const getEmployeesClient = () => new EmployeesClient('', axiosClient)
export const getTeamsClient = () => new TeamsClient('', axiosClient)
export const getTeamsOfTeamsClient = () =>
  new TeamsOfTeamsClient('', axiosClient)
export const getTeamMemberRolesClient = () =>
  new TeamMemberRolesClient('', axiosClient)

// PLANNING
export const getPlanningIntervalsClient = () =>
  new PlanningIntervalsClient('', axiosClient)
export const getRisksClient = () => new RisksClient('', axiosClient)
export const getRoadmapsClient = () => new RoadmapsClient('', axiosClient)
export const getSprintsClient = () => new SprintsClient('', axiosClient)
export const getEstimationScalesClient = () =>
  new EstimationScalesClient('', axiosClient)
export const getPokerSessionsClient = () =>
  new PokerSessionsClient('', axiosClient)

// PPM
export const getExpenditureCategoriesClient = () =>
  new ExpenditureCategoriesClient('', axiosClient)
export const getPortfoliosClient = () => new PortfoliosClient('', axiosClient)
export const getProgramsClient = () => new ProgramsClient('', axiosClient)
export const getProjectsClient = () => new ProjectsClient('', axiosClient)
export const getProjectHealthChecksClient = () =>
  new ProjectHealthChecksClient('', axiosClient)
export const getProjectScoresClient = () =>
  new ProjectScoresClient('', axiosClient)
export const getProjectTasksClient = () =>
  new ProjectTasksClient('', axiosClient)
export const getStrategicInitiativesClient = () =>
  new StrategicInitiativesClient('', axiosClient)
export const getProjectLifecyclesClient = () =>
  new ProjectLifecyclesClient('', axiosClient)
export const getScoringModelsClient = () =>
  new ScoringModelsClient('', axiosClient)

// SEARCH
export const getSearchClient = () => new SearchClient('', axiosClient)

// STRATEGIC MANAGEMENT
export const getStrategicThemesClient = () =>
  new StrategicThemesClient('', axiosClient)

// WORK MANAGEMENT
export const getWorkProcessesClient = () =>
  new WorkProcessesClient('', axiosClient)
export const getWorkspacesClient = () => new WorkspacesClient('', axiosClient)
export const getWorkStatusesClient = () =>
  new WorkStatusesClient('', axiosClient)
export const getWorkTypeLevelsClient = () =>
  new WorkTypeLevelsClient('', axiosClient)
export const getWorkTypesClient = () => new WorkTypesClient('', axiosClient)
export const getWorkTypeTiersClient = () =>
  new WorkTypeTiersClient('', axiosClient)

// FEATURE MANAGEMENT
export const getFeatureFlagsClient = () =>
  new FeatureFlagsClient('', axiosClient)

// USER MANAGEMENT
export const getOidcProvidersClient = () => new OidcProvidersClient('', axiosClient)
export const getPermissionsClient = () => new PermissionsClient('', axiosClient)
export const getProfileClient = () => new ProfileClient('', axiosClient)
export const getRolesClient = () => new RolesClient('', axiosClient)
export const getUsersClient = () => new UsersClient('', axiosClient)
export const getPersonalAccessTokensClient = () =>
  new PersonalAccessTokensClient('', axiosClient)

/**
 * Performs an authenticated fetch request.
 * Use this for custom API calls that need authentication but can't use the
 * generated clients (e.g., JSON Patch endpoints that NSwag doesn't generate).
 */
export async function authenticatedFetch(
  url: string,
  options: RequestInit = {},
): Promise<Response> {
  const token = getAuthToken()

  const headers = new Headers(options.headers)
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json')
  }

  const fullUrl = url.startsWith('http') ? url : `${apiUrl}${url}`

  return fetch(fullUrl, {
    ...options,
    headers,
    credentials: options.credentials || 'include',
  })
}
