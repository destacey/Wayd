/** An error response from the API: its status code and its parsed body. */
export type HttpError = {
  status: number | undefined
  data: unknown
}

/**
 * Reads an error a generated client function threw as `{ status, data }`, the
 * shape forms read `data.errors` and `data.detail` from.
 *
 * A failed response arrives as its problem details with its status (an
 * `ApiResponseError`), so the error is its own data. Returns `undefined` for
 * anything that is not an error response, such as a network failure or an
 * `Error` raised before sending, so callers keep their own fallback.
 */
export const toHttpError = (error: unknown): HttpError | undefined => {
  if (error === null || typeof error !== 'object' || error instanceof Error) {
    return undefined
  }
  const status = (error as { status?: unknown }).status
  return {
    status: typeof status === 'number' ? status : undefined,
    data: error,
  }
}
