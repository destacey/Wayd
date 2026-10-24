import { isNetworkError, isTransientRefreshFailure } from './request-failures'
import { ApiException } from './wayd-api'

const apiException = (status: number) =>
  new ApiException('An unexpected server error occurred.', status, '', {}, null)

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
  it.each([429, 500, 502, 503])(
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

  it.each([400, 403, 404, 428])(
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
