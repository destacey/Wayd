import type { ApiResponseError } from '@/src/services/api-fetch'
import { toHttpError } from './http-error'

const responseError = (
  status: number,
  problem: Record<string, unknown> = {},
): ApiResponseError => ({
  ...problem,
  kind: 'api-response',
  status,
  url: '/api/ppm/projects',
  headers: {},
})

describe('toHttpError', () => {
  it('reads a failed response as its status and problem details', () => {
    // Arrange
    const failure = responseError(422, {
      title: 'One or more validation errors occurred.',
      errors: { Name: ['Name is required.'] },
    })

    // Act
    const error = toHttpError(failure)

    // Assert
    expect(error).toEqual({ status: 422, data: failure })
    expect(error?.data).toMatchObject({
      errors: { Name: ['Name is required.'] },
    })
  })

  it('keeps a response with no problem details to its status', () => {
    // Arrange
    const failure = responseError(403)

    // Act
    const error = toHttpError(failure)

    // Assert — no detail, so the caller shows its own message
    expect(error?.status).toBe(403)
    expect(error?.data).not.toHaveProperty('detail')
  })

  it.each([
    ['a network failure', new TypeError('Failed to fetch')],
    ['an error raised before sending', new Error('Not sent.')],
  ])('leaves %s to the caller', (_, thrown) => {
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
