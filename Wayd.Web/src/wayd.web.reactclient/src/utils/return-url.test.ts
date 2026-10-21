import { storeReturnUrl, takeReturnUrl, toSafeReturnPath } from './return-url'

const KEY = 'wayd.returnUrl'

// jest.setup stubs sessionStorage with bare mocks; give them a backing store.
const store = new Map<string, string>()

describe('return-url', () => {
  beforeEach(() => {
    store.clear()
    jest
      .mocked(sessionStorage.getItem)
      .mockImplementation((key) => store.get(key) ?? null)
    jest
      .mocked(sessionStorage.setItem)
      .mockImplementation((key, value) => void store.set(key, value))
    jest
      .mocked(sessionStorage.removeItem)
      .mockImplementation((key) => void store.delete(key))
    window.history.replaceState(null, '', '/')
  })

  describe('storeReturnUrl', () => {
    it('keeps the query string with the path', () => {
      // Arrange
      window.history.replaceState(null, '', '/oauth/consent?request=abc123')

      // Act
      storeReturnUrl()

      // Assert
      expect(sessionStorage.getItem(KEY)).toBe('/oauth/consent?request=abc123')
    })

    it.each(['/', '/login', '/login?code=x', '/logout', '/setup'])(
      'does not store the sign-in page %s',
      (path) => {
        // Arrange
        sessionStorage.setItem(KEY, '/ppm/projects')
        window.history.replaceState(null, '', path)

        // Act
        storeReturnUrl()

        // Assert
        expect(sessionStorage.getItem(KEY)).toBe('/ppm/projects')
      },
    )
  })

  describe('takeReturnUrl', () => {
    it('returns the stored path and query and clears it', () => {
      // Arrange
      sessionStorage.setItem(KEY, '/oauth/consent?request=abc123')

      // Act
      const result = takeReturnUrl()

      // Assert
      expect(result).toBe('/oauth/consent?request=abc123')
      expect(sessionStorage.getItem(KEY)).toBeNull()
    })

    it('returns null when nothing is stored', () => {
      // Act
      const result = takeReturnUrl()

      // Assert
      expect(result).toBeNull()
    })

    it('refuses an off-site value someone stored and still clears it', () => {
      // Arrange
      sessionStorage.setItem(KEY, 'https://evil.example/phish')

      // Act
      const result = takeReturnUrl()

      // Assert
      expect(result).toBeNull()
      expect(sessionStorage.getItem(KEY)).toBeNull()
    })
  })

  describe('toSafeReturnPath', () => {
    it.each([
      ['https://evil.example/', 'an absolute URL'],
      ['//evil.example/path', 'a protocol-relative URL'],
      ['/\\evil.example/path', 'a backslash host'],
      ['javascript:alert(1)', 'a script URL'],
      ['/\t/evil.example', 'a control character'],
      ['projects', 'a path without a leading slash'],
      ['http://localhost/ppm/projects', 'an absolute URL on this origin'],
      ['/.//evil.example/x', 'a dot segment that leaves //host'],
      ['/a/..//evil.example', 'a parent segment that leaves //host'],
      ['/%2e//evil.example', 'an encoded dot segment that leaves //host'],
      ['', 'an empty value'],
    ])('refuses %s (%s)', (value) => {
      // Act
      const result = toSafeReturnPath(value)

      // Assert
      expect(result).toBeNull()
    })

    it('accepts a same-origin path with a query', () => {
      // Act
      const result = toSafeReturnPath('/ppm/projects/12?tab=tasks')

      // Assert
      expect(result).toBe('/ppm/projects/12?tab=tasks')
    })
  })
})
