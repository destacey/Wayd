import { toApiError } from '@/src/utils/problem-details'

const unreachable = new WeakSet<object>()

/**
 * Records an error `fetch` rejected with, so {@link isNetworkError} can tell a
 * failure to reach the server from a `TypeError` thrown by code around the
 * request, which must not be mistaken for one. Returns the error.
 */
export function markUnreachable(error: unknown): unknown {
  if (error && typeof error === 'object') unreachable.add(error)
  return error
}

/**
 * True for a failure where no response was received: offline, DNS failure,
 * server unreachable, request timed out, request aborted. We treat these as
 * "couldn't ask the server" rather than "server said no" — the distinction
 * matters when deciding whether a failed refresh proves the session is bad.
 *
 * `fetch` rejects with a `TypeError` when it cannot reach the server, so a
 * `TypeError` counts only when {@link markUnreachable} recorded it; a
 * `DOMException` named `TimeoutError` or `AbortError` means the signal fired.
 */
export function isNetworkError(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false
  if (error instanceof TypeError) return unreachable.has(error)
  const { name } = error as { name?: unknown }
  return name === 'TimeoutError' || name === 'AbortError'
}

/**
 * Logs a failed API request at the level its cause deserves. Only server
 * errors and unexplained failures are `console.error`, which Next's dev
 * overlay reports as a crash.
 *
 * A network error is a warning: RTK Query retries plus refetch-on-focus turn
 * one offline blip into dozens of identical entries. A 4xx is a warning too,
 * because only the caller knows whether it is a failure: a form shows its 400
 * or 422, a 404 can mean "nothing there" (a team with no active sprint), and a
 * 429 passes once its wait does. Each endpoint's queryFn logs a real failure
 * with its own context.
 */
export function logFailedRequest({
  error,
  url,
  status,
}: {
  error: unknown
  url?: string
  /** The response's status; undefined when there was no response. */
  status?: number
}): void {
  if (isNetworkError(error)) {
    console.warn('API unreachable:', url)
  } else if (status === 429) {
    console.warn('API rate limit reached:', url)
  } else if (status !== undefined && status >= 400 && status < 500) {
    console.warn('API refused the request:', status, url)
  } else {
    // A failed response carries its status and problem details rather than a
    // message; a thrown Error carries a message.
    const { message, detail } = (error ?? {}) as {
      message?: unknown
      detail?: unknown
    }
    console.error('API Error:', status ?? '', detail ?? message ?? '', url)
  }
}

const TRANSIENT_REFRESH_STATUSES = new Set([429, 502, 503, 504])

/**
 * True when a failed refresh-token call says nothing about whether the session
 * is still good, so the caller must keep it rather than sign the user out:
 * the server could not be reached, refused the call for now (429 — the
 * refresh endpoint is rate limited per address, so everyone behind one office
 * IP would otherwise be signed out together), or was unavailable (502, 503,
 * 504 — a gateway or a restarting replica).
 *
 * A plain 500 is not transient: refresh answers 500 for a token it cannot
 * validate (after the signing secret is rotated, say), which can never
 * succeed, so keeping that session would leave the user stuck rather than
 * signed out.
 *
 * A failed response carries its `status` (an `ApiResponseError`).
 */
export function isTransientRefreshFailure(error: unknown): boolean {
  if (isNetworkError(error)) return true
  if (!error || typeof error !== 'object') return false
  const { status } = error as { status?: unknown }
  if (typeof status !== 'number') return false
  return TRANSIENT_REFRESH_STATUSES.has(status)
}

/** How long to hold off after a 429 that does not say how long to wait. */
export const DEFAULT_RATE_LIMIT_WAIT_MS = 5_000

/** The longest hold-off honoured, so a bad header cannot stall refresh for long. */
const MAX_RATE_LIMIT_WAIT_MS = 5 * 60_000

/**
 * How long a 429 asks the caller to wait, in milliseconds, or `undefined` for
 * any other failure. Read from `Retry-After` (seconds) on the error's
 * `headers`, whose names are lowercased.
 */
export function rateLimitWaitMs(error: unknown): number | undefined {
  if (!error || typeof error !== 'object') return undefined
  const { status, headers } = error as {
    status?: unknown
    headers?: Record<string, unknown>
  }
  if (status !== 429) return undefined

  const seconds = Number(headers?.['retry-after'])
  if (!Number.isFinite(seconds) || seconds <= 0)
    return DEFAULT_RATE_LIMIT_WAIT_MS
  return Math.min(seconds * 1000, MAX_RATE_LIMIT_WAIT_MS)
}

/**
 * Remembers until when a refused request should not be sent again: after a
 * 429, until the wait it named has passed. Times are `Date.now()` values.
 */
export class RateLimitPause {
  private until = 0

  /** Whether the wait from the last 429 is still running at `now`. */
  isActive(now: number = Date.now()): boolean {
    return now < this.until
  }

  /** Starts a pause if `error` is a 429; any other failure leaves it as it was. */
  record(error: unknown, now: number = Date.now()): void {
    const wait = rateLimitWaitMs(error)
    if (wait !== undefined) this.until = now + wait
  }
}

/**
 * The message to show for a sign-in refused by a rate limit, or `undefined`
 * when the failure was something else. Prefers the server's own sentence,
 * which says how long to wait.
 */
export function signInRateLimitMessage(error: unknown): string | undefined {
  const apiError = toApiError(error)
  if (apiError.status !== 429) return undefined
  return apiError.detail ?? 'Too many sign-in attempts. Try again shortly.'
}
