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
export function isNetworkError(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false
  const e = error as { response?: unknown; code?: string; message?: string }
  if (e.response) return false
  if (
    e.code === 'ERR_NETWORK' ||
    e.code === 'ECONNABORTED' ||
    e.code === 'ERR_CANCELED'
  ) {
    return true
  }
  return e.message === 'Network Error'
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
 * The generated client throws an `ApiException` for a status it does not
 * document and the response body for one it does, so the status is read from
 * either: both carry `status`, an `ApiException` as a field and problem details
 * as part of the body.
 */
export function isTransientRefreshFailure(error: unknown): boolean {
  if (isNetworkError(error)) return true
  if (!error || typeof error !== 'object') return false
  const { status } = error as { status?: unknown }
  if (typeof status !== 'number') return false
  return TRANSIENT_REFRESH_STATUSES.has(status)
}
