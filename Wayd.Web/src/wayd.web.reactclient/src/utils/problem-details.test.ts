import { ApiException } from '@/src/services/wayd-api'
import toFormErrors, {
  errorsByField,
  isApiError,
  toApiError,
} from './problem-details'

describe('isApiError', () => {
  it('returns true for object with status', () => {
    expect(isApiError({ status: 422 })).toBe(true)
  })

  it('returns true for object with detail', () => {
    expect(isApiError({ detail: 'Not found' })).toBe(true)
  })

  it('returns true for object with errors', () => {
    expect(isApiError({ errors: { Name: ['required'] } })).toBe(true)
  })

  it('returns true for fully populated ApiError', () => {
    expect(
      isApiError({
        status: 422,
        detail: 'Validation failed',
        errors: { Key: ['must be unique'] },
      }),
    ).toBe(true)
  })

  it('returns false for null', () => {
    expect(isApiError(null)).toBe(false)
  })

  it('returns false for undefined', () => {
    expect(isApiError(undefined)).toBe(false)
  })

  it('returns false for a string', () => {
    expect(isApiError('something went wrong')).toBe(false)
  })

  it('returns false for a number', () => {
    expect(isApiError(500)).toBe(false)
  })

  it('returns false for an object without recognized keys', () => {
    expect(isApiError({ message: 'oops', code: 1 })).toBe(false)
  })

  it('narrows the type so ApiError fields are accessible after the guard', () => {
    const error: unknown = { status: 404, detail: 'Not found' }
    if (isApiError(error)) {
      expect(error.status).toBe(404)
      expect(error.detail).toBe('Not found')
    } else {
      fail('Expected isApiError to return true')
    }
  })
})

describe('toFormErrors', () => {
  it('should return correct form errors', () => {
    const problemDetails = {
      Name: ['Name is required'],
      Email: ['Email is required', 'Email is not valid'],
    }

    const result = toFormErrors(problemDetails)

    expect(result).toEqual(
      expect.arrayContaining([
        { name: 'Name', errors: ['Name is required'] },
        { name: 'name', errors: ['Name is required'] },
        {
          name: 'Email',
          errors: ['Email is required', 'Email is not valid'],
        },
        {
          name: 'email',
          errors: ['Email is required', 'Email is not valid'],
        },
      ]),
    )
  })

  it('should map dotted and jsonpath keys', () => {
    const problemDetails = {
      'request.Key': ['Key must be unique'],
      '$.key': ['Key must be 2-20 uppercase alphanumeric characters'],
    }

    const result = toFormErrors(problemDetails)

    expect(result).toEqual(
      expect.arrayContaining([
        { name: 'request.Key', errors: ['Key must be unique'] },
        { name: 'request.key', errors: ['Key must be unique'] },
        { name: 'Key', errors: ['Key must be unique'] },
        {
          name: 'key',
          errors: [
            'Key must be unique',
            'Key must be 2-20 uppercase alphanumeric characters',
          ],
        },
        {
          name: '$.key',
          errors: ['Key must be 2-20 uppercase alphanumeric characters'],
        },
      ]),
    )
  })

  it('should handle empty problem details', () => {
    const problemDetails = {}

    const result = toFormErrors(problemDetails)

    expect(result).toEqual([])
  })

  it('should handle null problem details', () => {
    const problemDetails = null

    const result = toFormErrors(problemDetails as any)

    expect(result).toEqual([])
  })

  it('should handle undefined problem details', () => {
    const problemDetails = undefined

    const result = toFormErrors(problemDetails as any)

    expect(result).toEqual([])
  })

  it('places an error only on the field the form maps its key to', () => {
    // Arrange
    const problemDetails = {
      '$.Start': ['Start is required'],
      Name: ['Name is required'],
    }

    // Act
    const result = toFormErrors(problemDetails, {
      fieldFor: (key) => (key === 'start' ? ['dates'] : undefined),
    })

    // Assert
    expect(result).toEqual([
      { name: ['dates'], errors: ['Start is required'] },
      { name: 'Name', errors: ['Name is required'] },
      { name: 'name', errors: ['Name is required'] },
    ])
  })
})

describe('errorsByField', () => {
  it('keeps only the keys the form maps, under its names', () => {
    // Arrange
    const problemDetails = {
      'Ratings[1].Value': ['Value is required'],
      'Ratings[1].RatingLevelId': ['Pick a level'],
      Id: ['Id is required'],
    }

    // Act
    const result = errorsByField(problemDetails, (key) =>
      key.startsWith('ratings[1].') ? 'criterion-2' : undefined,
    )

    // Assert
    expect(result).toEqual({
      'criterion-2': ['Value is required', 'Pick a level'],
    })
  })
})

describe('toApiError', () => {
  it('reads the problem details an undocumented response leaves as a string', () => {
    // Arrange
    const exception = new ApiException(
      'An unexpected server error occurred.',
      422,
      JSON.stringify({ errors: { End: ['End is required'] } }),
      {},
      null,
    )

    // Act
    const result = toApiError(exception)

    // Assert
    expect(result).toEqual({ status: 422, errors: { End: ['End is required'] } })
  })

  it('reads a body Axios has already parsed', () => {
    // Arrange
    const exception = new ApiException(
      'An unexpected server error occurred.',
      422,
      { errors: { End: ['End is required'] } } as unknown as string,
      {},
      null,
    )

    // Act
    const result = toApiError(exception)

    // Assert
    expect(result).toEqual({ status: 422, errors: { End: ['End is required'] } })
  })

  it('reads a plain-text body as the detail', () => {
    // Arrange
    const exception = new ApiException('Bad', 400, 'Not allowed.', {}, null)

    // Act / Assert
    expect(toApiError(exception)).toEqual({ status: 400, detail: 'Not allowed.' })
  })

  it('passes documented problem details through', () => {
    // Arrange
    const problem = { status: 422, errors: { Name: ['Name is required'] } }

    // Act / Assert
    expect(toApiError(problem)).toBe(problem)
  })

  it('reads anything else as no error response', () => {
    // Arrange / Act / Assert
    expect(toApiError(new Error('Network Error'))).toEqual({})
  })
})
