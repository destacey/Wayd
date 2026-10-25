import {
  DEFAULT_RATE_LIMIT_WAIT_MS,
  isNetworkError,
  isTransientRefreshFailure,
  RateLimitPause,
  signInRateLimitMessage,
  rateLimitWaitMs,
} from './request-failures'
import { ApiException } from './wayd-api'

const apiException = (
  status: number,
  headers: Record<string, string> = {},
  response = '',
) =>
  new ApiException(
    'An unexpected server error occurred.',
    status,
    response,
    headers,
    null,
  )

describe('isNetworkError', () => {
  it.each(['ERR_NETWORK', 'ECONNABORTED', 'ERR_CANCELED'])(
    'is true for an axios transport failure (%s)',
    (code) => {
      // Arrange
      const error = { code, message: 'whatever' }

      // Act
      const result = isNetworkError(error)

      // Assert
      expect(result).toBe(true)
    },
  )

  it('is false once the server has answered', () => {
    // Arrange
    const error = { code: 'ERR_NETWORK', response: { status: 503 } }

    // Act
    const result = isNetworkError(error)

    // Assert
    expect(result).toBe(false)
  })
})

describe('isTransientRefreshFailure', () => {
  it.each([429, 502, 503, 504])(
    'keeps the session when the refresh answers %i',
    (status) => {
      // Arrange
      const error = apiException(status)

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
      const error = apiException(status)

      // Act
      const result = isTransientRefreshFailure(error)

      // Assert
      expect(result).toBe(false)
    },
  )

  it('treats the documented 401, thrown as its problem details body, as a rejected session', () => {
    // Arrange — the generated client throws a documented response's body, not an ApiException
    const error = { status: 401, title: 'Unauthorized' }

    // Act
    const result = isTransientRefreshFailure(error)

    // Assert
    expect(result).toBe(false)
  })

  it('treats a 500 as a rejected session, because refresh answers it for a token it cannot validate', () => {
    // Arrange — a token signed with a rotated secret fails validation on every attempt
    const error = apiException(500)

    // Act
    const result = isTransientRefreshFailure(error)

    // Assert
    expect(result).toBe(false)
  })

  it('keeps the session when a documented 429 is thrown as its problem details body', () => {
    // Arrange
    const error = { status: 429, title: 'Too Many Requests' }

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
    const error = { code: 'ERR_NETWORK', message: 'Network Error' }

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
    const error = apiException(429, { 'retry-after': '12' })

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
      const error = apiException(429, headers)

      // Act
      const wait = rateLimitWaitMs(error)

      // Assert
      expect(wait).toBe(DEFAULT_RATE_LIMIT_WAIT_MS)
    },
  )

  it('caps a long wait at five minutes', () => {
    // Arrange
    const error = apiException(429, { 'retry-after': '86400' })

    // Act
    const wait = rateLimitWaitMs(error)

    // Assert
    expect(wait).toBe(5 * 60_000)
  })

  it.each([401, 500, 503])('is undefined for a %i', (status) => {
    // Arrange
    const error = apiException(status, { 'retry-after': '12' })

    // Act
    const wait = rateLimitWaitMs(error)

    // Assert
    expect(wait).toBeUndefined()
  })
})

describe('signInRateLimitMessage', () => {
  it("uses the server's sentence, which says how long to wait", () => {
    // Arrange
    const error = apiException(
      429,
      {},
      JSON.stringify({
        status: 429,
        detail: 'Too many requests. Try again in 9 seconds.',
      }),
    )

    // Act
    const message = signInRateLimitMessage(error)

    // Assert
    expect(message).toBe('Too many requests. Try again in 9 seconds.')
  })

  it('falls back to a general message for a 429 with no body', () => {
    // Arrange
    const error = apiException(429)

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
    pause.record(apiException(429, { 'retry-after': '10' }), 1_000)

    // Assert
    expect(pause.isActive(10_999)).toBe(true)
    expect(pause.isActive(11_000)).toBe(false)
  })

  it('is not started by any other failure', () => {
    // Arrange
    const pause = new RateLimitPause()

    // Act
    pause.record(apiException(503, { 'retry-after': '10' }), 1_000)

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
