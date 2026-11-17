import type { MockInstance } from 'vitest'
import {
  DEFAULT_RATE_LIMIT_WAIT_MS,
  isNetworkError,
  isTransientRefreshFailure,
  logFailedRequest,
  markUnreachable,
  RateLimitPause,
  signInRateLimitMessage,
  rateLimitWaitMs,
} from './request-failures'
import type { ApiResponseError } from './api-fetch'

/** What `fetch` rejects with when it cannot reach the server, as `apiFetch` records it. */
const unreachable = () => markUnreachable(new TypeError('Failed to fetch'))

/** What the generated client throws for a failed response. */
const responseError = (
  status: number,
  headers: Record<string, string> = {},
  problem: Record<string, unknown> = {},
): ApiResponseError => ({
  ...problem,
  kind: 'api-response',
  status,
  url: '/api/auth/refresh-token',
  headers,
})

describe('isNetworkError', () => {
  it('is true when fetch cannot reach the server', () => {
    // Arrange
    const error = unreachable()

    // Act
    const result = isNetworkError(error)

    // Assert
    expect(result).toBe(true)
  })

  it('is false for a TypeError fetch did not reject with', () => {
    // Arrange — a bug in code around the request, not a failed connection
    const error = new TypeError('Cannot read properties of undefined')

    // Act
    const result = isNetworkError(error)

    // Assert
    expect(result).toBe(false)
  })

  it.each(['TimeoutError', 'AbortError'])(
    'is true when the request signal fires (%s)',
    (name) => {
      // Arrange
      const error = new DOMException('signal fired', name)

      // Act
      const result = isNetworkError(error)

      // Assert
      expect(result).toBe(true)
    },
  )

  it('is false once the server has answered', () => {
    // Arrange
    const error = responseError(503)

    // Act
    const result = isNetworkError(error)

    // Assert
    expect(result).toBe(false)
  })
})

describe('logFailedRequest', () => {
  const url = '/api/organization/teams/team-1/sprints/active'
  let warn: MockInstance
  let error: MockInstance

  beforeEach(() => {
    warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    error = vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    warn.mockRestore()
    error.mockRestore()
  })

  it.each([400, 403, 404, 409, 422, 429])(
    'warns, not errors, for a %s the caller decides about',
    (status) => {
      // Arrange
      const failure = { error: { message: 'failed' }, url, status }

      // Act
      logFailedRequest(failure)

      // Assert
      expect(warn).toHaveBeenCalledTimes(1)
      expect(error).not.toHaveBeenCalled()
    },
  )

  it('warns for a request that never reached the server', () => {
    // Arrange
    const failure = { error: unreachable(), url }

    // Act
    logFailedRequest(failure)

    // Assert
    expect(warn).toHaveBeenCalledWith('API unreachable:', url)
    expect(error).not.toHaveBeenCalled()
  })

  it.each([500, 503])('errors for a server error (%s)', (status) => {
    // Arrange
    const failure = { error: { message: 'failed' }, url, status }

    // Act
    logFailedRequest(failure)

    // Assert
    expect(error).toHaveBeenCalledWith('API Error:', status, 'failed', url)
    expect(warn).not.toHaveBeenCalled()
  })

  it('logs a failed response by its status and detail, which it has instead of a message', () => {
    // Arrange
    const failure = {
      error: { status: 500, detail: 'The database is unavailable.' },
      url,
      status: 500,
    }

    // Act
    logFailedRequest(failure)

    // Assert
    expect(error).toHaveBeenCalledWith(
      'API Error:',
      500,
      'The database is unavailable.',
      url,
    )
  })
})

describe('isTransientRefreshFailure', () => {
  it.each([429, 502, 503, 504])(
    'keeps the session when the refresh answers %i',
    (status) => {
      // Arrange
      const error = responseError(status)

      // Act
      const result = isTransientRefreshFailure(error)

      // Assert
      expect(result).toBe(true)
    },
  )

  it.each([400, 403, 404, 428, 501])(
    'treats a %i from the refresh as a rejected session',
    (status) => {
      // Arrange
      const error = responseError(status)

      // Act
      const result = isTransientRefreshFailure(error)

      // Assert
      expect(result).toBe(false)
    },
  )

  it('treats a 401 as a rejected session', () => {
    // Arrange
    const error = responseError(401, {}, { title: 'Unauthorized' })

    // Act
    const result = isTransientRefreshFailure(error)

    // Assert
    expect(result).toBe(false)
  })

  it('treats a 500 as a rejected session, because refresh answers it for a token it cannot validate', () => {
    // Arrange — a token signed with a rotated secret fails validation on every attempt
    const error = responseError(500)

    // Act
    const result = isTransientRefreshFailure(error)

    // Assert
    expect(result).toBe(false)
  })

  it('keeps the session when the refresh answers 429', () => {
    // Arrange
    const error = responseError(429, {}, { title: 'Too Many Requests' })

    // Act
    const result = isTransientRefreshFailure(error)

    // Assert
    expect(result).toBe(true)
  })

  it('treats a failure with no status as a rejected session', () => {
    // Arrange
    const error = { message: 'Something went wrong' }

    // Act
    const result = isTransientRefreshFailure(error)

    // Assert
    expect(result).toBe(false)
  })

  it('keeps the session when the server could not be reached', () => {
    // Arrange
    const error = unreachable()

    // Act
    const result = isTransientRefreshFailure(error)

    // Assert
    expect(result).toBe(true)
  })

  it.each([null, undefined, 'boom'])(
    'treats a non-object failure (%p) as a rejected session',
    (error) => {
      // Act
      const result = isTransientRefreshFailure(error)

      // Assert
      expect(result).toBe(false)
    },
  )
})

describe('rateLimitWaitMs', () => {
  it('reads the wait from Retry-After, in seconds', () => {
    // Arrange
    const error = responseError(429, { 'retry-after': '12' })

    // Act
    const wait = rateLimitWaitMs(error)

    // Assert
    expect(wait).toBe(12_000)
  })

  it.each<Record<string, string>>([
    {},
    { 'retry-after': '0' },
    { 'retry-after': 'soon' },
  ])(
    'holds off for the default when a 429 names no usable wait (%p)',
    (headers) => {
      // Arrange
      const error = responseError(429, headers)

      // Act
      const wait = rateLimitWaitMs(error)

      // Assert
      expect(wait).toBe(DEFAULT_RATE_LIMIT_WAIT_MS)
    },
  )

  it('caps a long wait at five minutes', () => {
    // Arrange
    const error = responseError(429, { 'retry-after': '86400' })

    // Act
    const wait = rateLimitWaitMs(error)

    // Assert
    expect(wait).toBe(5 * 60_000)
  })

  it.each([401, 500, 503])('is undefined for a %i', (status) => {
    // Arrange
    const error = responseError(status, { 'retry-after': '12' })

    // Act
    const wait = rateLimitWaitMs(error)

    // Assert
    expect(wait).toBeUndefined()
  })
})

describe('signInRateLimitMessage', () => {
  it("uses the server's sentence, which says how long to wait", () => {
    // Arrange
    const error = responseError(
      429,
      {},
      { detail: 'Too many requests. Try again in 9 seconds.' },
    )

    // Act
    const message = signInRateLimitMessage(error)

    // Assert
    expect(message).toBe('Too many requests. Try again in 9 seconds.')
  })

  it('falls back to a general message for a 429 with no body', () => {
    // Arrange
    const error = responseError(429)

    // Act
    const message = signInRateLimitMessage(error)

    // Assert
    expect(message).toBe('Too many sign-in attempts. Try again shortly.')
  })

  it('is undefined for any other failure, so the caller keeps its own message', () => {
    // Arrange
    const error = { status: 401, detail: 'Invalid email or password.' }

    // Act
    const message = signInRateLimitMessage(error)

    // Assert
    expect(message).toBeUndefined()
  })
})

describe('RateLimitPause', () => {
  it('holds off until the wait a 429 named has passed', () => {
    // Arrange
    const pause = new RateLimitPause()

    // Act
    pause.record(responseError(429, { 'retry-after': '10' }), 1_000)

    // Assert
    expect(pause.isActive(10_999)).toBe(true)
    expect(pause.isActive(11_000)).toBe(false)
  })

  it('is not started by any other failure', () => {
    // Arrange
    const pause = new RateLimitPause()

    // Act
    pause.record(responseError(503, { 'retry-after': '10' }), 1_000)

    // Assert
    expect(pause.isActive(1_001)).toBe(false)
  })

  it('is inactive before anything is refused', () => {
    // Act
    const active = new RateLimitPause().isActive()

    // Assert
    expect(active).toBe(false)
  })
})
