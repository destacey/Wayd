import { ApiException } from '@/src/services/wayd-api'
import { toHttpError } from './http-error'

describe('toHttpError', () => {
  it('reads a problem details body the client threw as the response', () => {
    // Arrange
    const problem = {
      status: 422,
      title: 'One or more validation errors occurred.',
      errors: { Name: ['Name is required.'] },
    }

    // Act
    const error = toHttpError(problem)

    // Assert
    expect(error).toEqual({ status: 422, data: problem })
  })

  it('wraps a plain-text body as its detail', () => {
    // Arrange
    const body = "Project stage with ID 'abc' not found."

    // Act
    const error = toHttpError(body)

    // Assert — a plain body carries no status of its own.
    expect(error).toEqual({ status: undefined, data: { detail: body } })
  })

  it('takes the status and parsed body of an undocumented response', () => {
    // Arrange
    const exception = new ApiException(
      'An unexpected server error occurred.',
      500,
      '{"title":"Server error","status":500}',
      {},
      null,
    )

    // Act
    const error = toHttpError(exception)

    // Assert
    expect(error).toEqual({
      status: 500,
      data: { title: 'Server error', status: 500 },
    })
  })

  it('keeps an undocumented response body that is not JSON as its detail', () => {
    // Arrange
    const exception = new ApiException('Forbidden', 403, 'Forbidden', {}, null)

    // Act
    const error = toHttpError(exception)

    // Assert
    expect(error).toEqual({ status: 403, data: { detail: 'Forbidden' } })
  })

  it('leaves a network failure to the caller', () => {
    // Arrange
    const networkError = { isAxiosError: true, message: 'Network Error' }

    // Act
    const error = toHttpError(networkError)

    // Assert
    expect(error).toBeUndefined()
  })

  it('takes an undocumented response body axios already parsed as it is', () => {
    // Arrange
    const body = { title: 'Server error', status: 500 }
    const exception = new ApiException(
      'An unexpected server error occurred.',
      500,
      body as unknown as string,
      {},
      null,
    )

    // Act
    const error = toHttpError(exception)

    // Assert
    expect(error).toEqual({ status: 500, data: body })
  })

  it('gives an empty body no detail, so the caller shows its own message', () => {
    // Arrange
    const exception = new ApiException('Forbidden', 403, '', {}, null)

    // Act
    const error = toHttpError(exception)

    // Assert
    expect(error).toEqual({ status: 403, data: {} })
  })

  it('leaves an error the client raised before sending to the caller', () => {
    // Arrange
    const thrown = new Error("The parameter 'id' must be defined.")

    // Act
    const error = toHttpError(thrown)

    // Assert
    expect(error).toBeUndefined()
  })

  it.each([null, undefined])(
    'leaves a rejection with no reason (%p) to the caller',
    (reason) => {
      // Arrange / Act
      const error = toHttpError(reason)

      // Assert
      expect(error).toBeUndefined()
    },
  )
})
