import { ApiException } from '@/src/services/wayd-api'

/** An error response from the API: its status code and its parsed body. */
export type HttpError = {
  status: number | undefined
  data: unknown
}

// An empty body (a bare 403, say) carries no detail, so callers fall back to
// their own message rather than showing an empty one.
const textBody = (text: string): object =>
  text.trim() === '' ? {} : { detail: text }

const parseBody = (body: unknown): unknown => {
  if (typeof body !== 'string') return body
  try {
    return JSON.parse(body)
  } catch {
    return textBody(body)
  }
}

/**
 * Reads an error a generated client method threw as `{ status, data }`, the
 * shape forms read `data.errors` and `data.detail` from.
 *
 * The generated client throws a documented error response's body itself — a
 * problem details object, or the text of a plain-string body, which carries no
 * status — and an `ApiException` for an undocumented one. Returns `undefined`
 * for anything that is not an error response, such as a network failure or an
 * `Error` the client raised before sending, so callers keep their own fallback.
 */
export const toHttpError = (error: unknown): HttpError | undefined => {
  if (error === null || error === undefined) return undefined
  if (ApiException.isApiException(error)) {
    return { status: error.status, data: parseBody(error.response) }
  }
  if (error instanceof Error) return undefined
  if (typeof error === 'string') {
    return { status: undefined, data: textBody(error) }
  }
  if (typeof error === 'object' && !('isAxiosError' in error)) {
    const status = (error as { status?: unknown }).status
    return {
      status: typeof status === 'number' ? status : undefined,
      data: error,
    }
  }
  return undefined
}
