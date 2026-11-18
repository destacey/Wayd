import {
  currentBrowserTrace,
  installBrowserTraceCapture,
} from '@/src/otel/browser-traceparent'
import { storeReturnUrl } from '@/src/utils/return-url'
import { navigateWithFullReload } from '@/src/utils/window-utils'
// Type-only: the generated endpoints import this module, so a value import
// from them would be a cycle.
import type { TokenResponse } from './api/wayd.schemas'
import {
  clearAuth,
  getAuthRefreshToken,
  getAuthToken,
  isAuthTokenExpiringSoon,
  storeAuth,
} from './auth-storage'
import {
  isTransientRefreshFailure,
  logFailedRequest,
  markUnreachable,
  RateLimitPause,
} from './request-failures'

const apiUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? ''

const REQUEST_TIMEOUT_MS = 30_000

/** A `.` or `..` path segment, which fetch resolves before sending. */
const DOT_SEGMENT = /(^|\/)\.{1,2}(\/|$)/

/**
 * What the generated functions throw for a response outside 2xx: the
 * response's problem details with its status, so `error.status`,
 * `error.detail` and `error.errors` read the same way whether or not the
 * endpoint documents that status. A body that is not an object (plain text,
 * or none) becomes `detail` when it has any text.
 *
 * It is a plain object rather than an `Error` because RTK Query keeps it in
 * the store, whose serializability check rejects class instances.
 */
export interface ApiResponseError {
  kind: 'api-response'
  status: number
  /** The request path, without the API base URL. */
  url: string
  /** Response headers, names lowercased. */
  headers: Record<string, string>
  detail?: string
  title?: string
  errors?: { [key: string]: string[] }
  [key: string]: unknown
}

/** True for an error a generated function threw for a non-2xx response. */
export const isApiResponseError = (error: unknown): error is ApiResponseError =>
  typeof error === 'object' &&
  error !== null &&
  (error as { kind?: unknown }).kind === 'api-response'

const readBody = async (response: Response): Promise<unknown> => {
  // An image (an attachment) is bytes; reading it as text would corrupt it.
  if (response.ok && response.headers.get('content-type')?.startsWith('image/'))
    return response.blob()
  const text = await response.text()
  if (!text) return undefined
  const contentType = response.headers.get('content-type') ?? ''
  // application/json and application/problem+json
  if (!contentType.includes('json')) return text
  try {
    return JSON.parse(text)
  } catch {
    // A gateway or proxy can label an HTML or truncated error page JSON.
    // Reading it as text keeps the response's status, which decides whether a
    // failed refresh signs the user out.
    return text
  }
}

const toResponseError = (
  url: string,
  response: Response,
  body: unknown,
): ApiResponseError => {
  const headers: Record<string, string> = {}
  response.headers.forEach((value, name) => {
    headers[name.toLowerCase()] = value
  })
  const problem =
    typeof body === 'object' && body !== null && !Array.isArray(body)
      ? body
      : typeof body === 'string' && body.trim()
        ? { detail: body }
        : {}
  return {
    ...problem,
    kind: 'api-response',
    status: response.status,
    url,
    headers,
  }
}

type Credentials =
  /** The signed-in session: proactive refresh, and one retry after a 401. */
  | 'session'
  /** None: the endpoint is anonymous. */
  | 'anonymous'
  /** The stored token exactly as it is, never refreshed. */
  | 'stored-token'

interface SendResult {
  response: Response
  body: unknown
}

const send = async (
  url: string,
  init: RequestInit,
  token: string | null,
  traced: boolean,
): Promise<SendResult> => {
  // encodeURIComponent leaves a path parameter of `..` as it is, so an id read
  // from a URL could otherwise move the request, and its token, to another
  // endpoint.
  if (DOT_SEGMENT.test(url.split('?')[0])) {
    throw new Error(`Refused to send a request with a dot segment: ${url}`)
  }
  const headers = new Headers(init.headers)
  if (!headers.has('Accept')) headers.set('Accept', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (traced) {
    // Browser calls to the API are a new trace unless they carry the page
    // span. The server injects that context; this is where it leaves.
    const trace = currentBrowserTrace()
    if (trace && !headers.has('traceparent')) {
      headers.set('traceparent', trace.traceparent)
      if (trace.tracestate) headers.set('tracestate', trace.tracestate)
    }
  }

  const timeout = AbortSignal.timeout(REQUEST_TIMEOUT_MS)
  const signal = init.signal ? AbortSignal.any([init.signal, timeout]) : timeout

  let response: Response
  try {
    response = await fetch(`${apiUrl}${url}`, { ...init, headers, signal })
  } catch (error) {
    throw markUnreachable(error)
  }
  return { response, body: await readBody(response) }
}

const request = async <T>(
  url: string,
  init: RequestInit,
  credentials: Credentials,
): Promise<T> => {
  if (credentials !== 'session') {
    const token = credentials === 'stored-token' ? getAuthToken() : null
    const { response, body } = await send(url, init, token, false)
    if (!response.ok) throw toResponseError(url, response, body)
    return body as T
  }

  installBrowserTraceCapture()
  let result: SendResult
  try {
    result = await send(url, init, await getFreshAuthToken(), true)
  } catch (error) {
    logFailedRequest({ error, url })
    throw error
  }

  if (result.response.status === 401) {
    result = await recoverFromUnauthorized(url, init, result)
  }

  if (!result.response.ok) {
    const error = toResponseError(url, result.response, result.body)
    logFailedRequest({ error, url, status: error.status })
    throw error
  }
  return result.body as T
}

/**
 * Refreshes the session after a 401 and sends the request once more, or, when
 * the session cannot be recovered, signs out and throws.
 */
const recoverFromUnauthorized = async (
  url: string,
  init: RequestInit,
  unauthorized: SendResult,
): Promise<SendResult> => {
  const unauthorizedError = toResponseError(
    url,
    unauthorized.response,
    unauthorized.body,
  )

  const refresh = refreshOnce()
  if (!refresh) {
    // No tokens to refresh with: the caller was already unauthenticated.
    // Keep storage so the remember-me preference survives.
    redirectToLoginWithReturnUrl()
    throw unauthorizedError
  }

  let tokenResponse: TokenResponse
  try {
    tokenResponse = await refresh
  } catch (refreshError) {
    // A transient failure proves nothing about the session — signing out on
    // every wifi blip would be worse than one failed call — so the caller gets
    // its own 401, not the refresh endpoint's error.
    if (isTransientRefreshFailure(refreshError)) {
      console.warn(
        'Token refresh on 401 failed transiently; leaving session intact:',
        refreshError,
      )
      throw unauthorizedError
    }
    // Reachable and refused: the session is invalid.
    console.error('Token refresh on 401 failed:', refreshError)
    clearAuth()
    redirectToLoginWithReturnUrl()
    throw refreshError
  }

  try {
    return await send(url, init, tokenResponse.token, true)
  } catch (error) {
    logFailedRequest({ error, url })
    throw error
  }
}

function redirectToLoginWithReturnUrl(): void {
  if (typeof window === 'undefined') return
  storeReturnUrl()
  navigateWithFullReload('/login')
}

// When several requests 401 together (every in-flight call after an idle
// period), they share one refresh instead of each rotating the refresh token
// and orphaning the others' rotations.
//
// After a refresh is refused with 429, none is sent again until the wait it
// named has passed: the refresh limit is shared by everyone behind one
// address. A refresh asked for meanwhile fails as a 429, which callers treat
// as transient.
let inFlightRefresh: Promise<TokenResponse> | null = null
const refreshPause = new RateLimitPause()

/**
 * The single in-flight refresh of the stored session, or `null` when there are
 * no tokens to refresh with. `null` means "not a session" — the caller must
 * not clear storage — whereas a rejection means the refresh failed.
 */
export function refreshOnce(): Promise<TokenResponse> | null {
  if (inFlightRefresh) return inFlightRefresh

  const currentToken = getAuthToken()
  const refreshToken = getAuthRefreshToken()
  if (!currentToken || !refreshToken) return null

  if (refreshPause.isActive()) {
    return Promise.reject({
      status: 429,
      message: 'Token refresh paused by a rate limit.',
    })
  }

  inFlightRefresh = anonymousApiFetch<TokenResponse>(
    '/api/auth/refresh-token',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: currentToken, refreshToken }),
    },
  )
    .then((tokenResponse) => {
      storeAuth(tokenResponse)
      return tokenResponse
    })
    .catch((error: unknown) => {
      refreshPause.record(error)
      throw error
    })
    .finally(() => {
      inFlightRefresh = null
    })

  return inFlightRefresh
}

/**
 * The current Wayd JWT, refreshed first if it is expired or about to be.
 * Shared by API requests and the SignalR `accessTokenFactory`, so a hub
 * (re)connect, which happens outside the request pipeline, joins the same
 * single-flight refresh instead of handing SignalR a stale token.
 *
 * A failed refresh is not fatal: the stored token is returned anyway, and the
 * request's own 401 handling decides whether the session is over.
 */
export async function getFreshAuthToken(): Promise<string | null> {
  if (isAuthTokenExpiringSoon()) {
    const refresh = refreshOnce()
    if (refresh) {
      try {
        await refresh
      } catch {
        // Deliberate — see above.
      }
    }
  }
  return getAuthToken()
}

/**
 * The mutator for every generated API function that `orval.config.ts` does not
 * route elsewhere: sends the signed-in session's token, refreshing it ahead of
 * expiry and once after a 401.
 */
export const apiFetch = <T>(url: string, init: RequestInit): Promise<T> =>
  request<T>(url, init, 'session')

/**
 * The mutator for the anonymous auth endpoints (login, refresh, exchange,
 * providers, setup). They send no token, so they must not trigger a refresh or
 * the sign-out a 401 causes.
 */
export const anonymousApiFetch = <T>(
  url: string,
  init: RequestInit,
): Promise<T> => request<T>(url, init, 'anonymous')

/**
 * The mutator for sign-out. It sends the stored token as it is: refreshing
 * would mint a token pair moments before logout revokes it, and the session
 * path's 401 handling would redirect with a return URL, landing the user back
 * on the page they just left. An expired token is exactly what the server
 * should reject.
 */
export const logoutApiFetch = <T>(url: string, init: RequestInit): Promise<T> =>
  request<T>(url, init, 'stored-token')
