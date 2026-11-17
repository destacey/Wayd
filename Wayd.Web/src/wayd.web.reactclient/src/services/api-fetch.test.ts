// @vitest-environment jsdom
import type { MockInstance } from 'vitest'
import { navigateWithFullReload } from '@/src/utils/window-utils'
import {
  anonymousApiFetch,
  apiFetch,
  isApiResponseError,
  logoutApiFetch,
} from './api-fetch'
import { asInstant } from '@/src/utils/instant'

vi.mock(import('@/src/utils/window-utils'), async (importOriginal) => ({
  ...(await importOriginal()),
  navigateWithFullReload: vi.fn(),
}))

const json = (body: unknown, status = 200, headers: HeadersInit = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  })

const refreshed = {
  token: 'new-token',
  refreshToken: 'new-refresh-token',
  tokenExpiresAt: asInstant('2099-01-01T00:00:00.000Z'),
  mustChangePassword: false,
}

let fetchSpy: MockInstance<typeof fetch>

const authorization = (call: number) =>
  new Headers(fetchSpy.mock.calls[call][1]?.headers).get('Authorization')

const urlOf = (call: number) => String(fetchSpy.mock.calls[call][0])

const storeSession = (expiry = '2099-01-01T00:00:00.000Z') => {
  localStorage.setItem('wayd.local.token', 'old-token')
  localStorage.setItem('wayd.local.refreshToken', 'refresh-token')
  localStorage.setItem('wayd.local.tokenExpiry', expiry)
}

const caught = (call: Promise<unknown>) => call.catch((e: unknown) => e)

beforeEach(() => {
  fetchSpy = vi.spyOn(globalThis, 'fetch')
  vi.spyOn(console, 'warn').mockImplementation(() => {})
  vi.spyOn(console, 'error').mockImplementation(() => {})
})

afterEach(() => {
  vi.restoreAllMocks()
  vi.mocked(navigateWithFullReload).mockReset()
  localStorage.clear()
  sessionStorage.clear()
})

describe('apiFetch', () => {
  it('returns the parsed body and sends the stored token', async () => {
    // Arrange
    storeSession()
    fetchSpy.mockResolvedValue(json({ id: 'p-1' }))

    // Act
    const result = await apiFetch('/api/ppm/projects/p-1', { method: 'GET' })

    // Assert
    expect(result).toEqual({ id: 'p-1' })
    expect(urlOf(0).endsWith('/api/ppm/projects/p-1')).toBe(true)
    expect(authorization(0)).toBe('Bearer old-token')
  })

  it('returns undefined for an empty response', async () => {
    // Arrange
    storeSession()
    fetchSpy.mockResolvedValue(new Response(null, { status: 204 }))

    // Act
    const result = await apiFetch('/api/ppm/projects/p-1', {
      method: 'DELETE',
    })

    // Assert
    expect(result).toBeUndefined()
  })

  it('throws the problem details with the status for a failed response', async () => {
    // Arrange
    storeSession()
    fetchSpy.mockResolvedValue(
      json(
        { detail: 'Name is required.', errors: { Name: ['Required'] } },
        422,
        { 'Content-Type': 'application/problem+json', 'X-Trace': 'abc' },
      ),
    )

    // Act
    const error = await caught(
      apiFetch('/api/ppm/projects', { method: 'POST' }),
    )

    // Assert
    expect(isApiResponseError(error)).toBe(true)
    expect(error).toMatchObject({
      status: 422,
      detail: 'Name is required.',
      errors: { Name: ['Required'] },
      url: '/api/ppm/projects',
      headers: { 'x-trace': 'abc' },
    })
  })

  it('keeps the status of a response whose JSON body does not parse', async () => {
    // Arrange — a gateway's HTML error page, labelled JSON
    storeSession()
    fetchSpy.mockResolvedValue(
      new Response('<html>Bad Gateway</html>', {
        status: 502,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    // Act
    const error = await caught(apiFetch('/api/ppm/projects', {}))

    // Assert
    expect(isApiResponseError(error)).toBe(true)
    expect(error).toMatchObject({
      status: 502,
      detail: '<html>Bad Gateway</html>',
    })
  })

  it.each(['/api/organization/teams/..', '/api/organization/teams/./members'])(
    'refuses a path with a dot segment (%s)',
    async (url) => {
      // Arrange
      storeSession()

      // Act
      const error = await caught(apiFetch(url, {}))

      // Assert
      expect(error).toBeInstanceOf(Error)
      expect(fetchSpy).not.toHaveBeenCalled()
    },
  )

  it('throws a plain-text body as the detail', async () => {
    // Arrange
    storeSession()
    fetchSpy.mockResolvedValue(
      new Response('Project not found.', {
        status: 404,
        headers: { 'Content-Type': 'text/plain' },
      }),
    )

    // Act
    const error = await caught(apiFetch('/api/ppm/projects/x', {}))

    // Assert
    expect(error).toMatchObject({ status: 404, detail: 'Project not found.' })
  })

  it('refreshes the token after a 401 and sends the request once more', async () => {
    // Arrange
    storeSession()
    fetchSpy
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(json(refreshed))
      .mockResolvedValueOnce(json({ id: 'p-1' }))

    // Act
    const result = await apiFetch('/api/ppm/projects/p-1', { method: 'GET' })

    // Assert
    expect(result).toEqual({ id: 'p-1' })
    expect(urlOf(1).endsWith('/api/auth/refresh-token')).toBe(true)
    expect(authorization(1)).toBeNull()
    expect(authorization(2)).toBe('Bearer new-token')
    expect(localStorage.getItem('wayd.local.token')).toBe('new-token')
  })

  it('retries only once when the refreshed token is refused too', async () => {
    // Arrange
    storeSession()
    fetchSpy
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(json(refreshed))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))

    // Act
    const error = await caught(apiFetch('/api/ppm/projects', {}))

    // Assert
    expect(error).toMatchObject({ status: 401 })
    expect(fetchSpy).toHaveBeenCalledTimes(3)
  })

  it('shares one refresh between requests that 401 together', async () => {
    // Arrange
    storeSession()
    fetchSpy.mockImplementation(async (input, init) => {
      if (String(input).endsWith('/api/auth/refresh-token')) {
        return json(refreshed)
      }
      return new Headers(init?.headers).get('Authorization') ===
        'Bearer new-token'
        ? json({ ok: true })
        : new Response(null, { status: 401 })
    })

    // Act
    await Promise.all([
      apiFetch('/api/a', {}),
      apiFetch('/api/b', {}),
      apiFetch('/api/c', {}),
    ])

    // Assert
    const refreshes = fetchSpy.mock.calls.filter(([url]) =>
      String(url).endsWith('/api/auth/refresh-token'),
    )
    expect(refreshes).toHaveLength(1)
  })

  it('signs out when the server refuses the refresh', async () => {
    // Arrange
    storeSession()
    fetchSpy
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(json({ detail: 'Invalid token.' }, 400))

    // Act
    const error = await caught(apiFetch('/api/ppm/projects', {}))

    // Assert
    expect(error).toMatchObject({
      status: 400,
      url: '/api/auth/refresh-token',
    })
    expect(localStorage.getItem('wayd.local.token')).toBeNull()
    expect(navigateWithFullReload).toHaveBeenCalledWith('/login')
  })

  it('logs a network failure on the request it retries after a refresh', async () => {
    // Arrange
    storeSession()
    fetchSpy
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(json(refreshed))
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))

    // Act
    const error = await caught(apiFetch('/api/ppm/projects', {}))

    // Assert
    expect(error).toBeInstanceOf(TypeError)
    expect(console.warn).toHaveBeenCalledWith(
      'API unreachable:',
      '/api/ppm/projects',
    )
  })

  it('keeps the session and throws the original 401 when the refresh fails transiently', async () => {
    // Arrange
    storeSession()
    fetchSpy
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(null, { status: 503 }))

    // Act
    const error = await caught(apiFetch('/api/ppm/projects', {}))

    // Assert
    expect(error).toMatchObject({ status: 401, url: '/api/ppm/projects' })
    expect(localStorage.getItem('wayd.local.token')).toBe('old-token')
    expect(navigateWithFullReload).not.toHaveBeenCalled()
  })

  it('keeps the session when the refresh answers 502 with a body that does not parse', async () => {
    // Arrange
    storeSession()
    fetchSpy
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(
        new Response('<html>Bad Gateway</html>', {
          status: 502,
          headers: { 'Content-Type': 'application/json' },
        }),
      )

    // Act
    const error = await caught(apiFetch('/api/ppm/projects', {}))

    // Assert
    expect(error).toMatchObject({ status: 401, url: '/api/ppm/projects' })
    expect(localStorage.getItem('wayd.local.token')).toBe('old-token')
    expect(navigateWithFullReload).not.toHaveBeenCalled()
  })

  it('redirects without clearing storage when there is no session to refresh', async () => {
    // Arrange
    localStorage.setItem('wayd.local.rememberMe', 'true')
    fetchSpy.mockResolvedValueOnce(new Response(null, { status: 401 }))

    // Act
    const error = await caught(apiFetch('/api/ppm/projects', {}))

    // Assert
    expect(error).toMatchObject({ status: 401 })
    expect(fetchSpy).toHaveBeenCalledTimes(1)
    expect(navigateWithFullReload).toHaveBeenCalledWith('/login')
    expect(localStorage.getItem('wayd.local.rememberMe')).toBe('true')
  })

  it('refreshes before sending when the token is about to expire', async () => {
    // Arrange
    storeSession('2000-01-01T00:00:00.000Z')
    fetchSpy
      .mockResolvedValueOnce(json(refreshed))
      .mockResolvedValueOnce(json({ ok: true }))

    // Act
    await apiFetch('/api/ppm/projects', {})

    // Assert
    expect(urlOf(0).endsWith('/api/auth/refresh-token')).toBe(true)
    expect(authorization(1)).toBe('Bearer new-token')
  })

  it("aborts the request when the caller's signal fires", async () => {
    // Arrange
    storeSession()
    const controller = new AbortController()
    fetchSpy.mockImplementation(
      (_input, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () =>
            reject(init.signal?.reason),
          )
        }),
    )

    // Act
    const call = caught(
      apiFetch('/api/ppm/projects', { signal: controller.signal }),
    )
    // Let the proactive-refresh check finish so fetch has been called.
    await vi.waitFor(() => expect(fetchSpy).toHaveBeenCalled())
    controller.abort()
    const error = await call

    // Assert
    expect(error).toMatchObject({ name: 'AbortError' })
  })
})

describe('anonymousApiFetch', () => {
  it('sends no token and leaves the session alone on a 401', async () => {
    // Arrange
    storeSession()
    fetchSpy.mockResolvedValue(json({ detail: 'Wrong password.' }, 401))

    // Act
    const error = await caught(
      anonymousApiFetch('/api/auth/login', { method: 'POST' }),
    )

    // Assert
    expect(error).toMatchObject({ status: 401, detail: 'Wrong password.' })
    expect(fetchSpy).toHaveBeenCalledTimes(1)
    expect(authorization(0)).toBeNull()
    expect(navigateWithFullReload).not.toHaveBeenCalled()
  })
})

describe('logoutApiFetch', () => {
  it('sends the stored token without refreshing it, even when it has expired', async () => {
    // Arrange
    storeSession('2000-01-01T00:00:00.000Z')
    fetchSpy.mockResolvedValue(new Response(null, { status: 401 }))

    // Act
    const error = await caught(
      logoutApiFetch('/api/auth/logout', { method: 'POST' }),
    )

    // Assert
    expect(error).toMatchObject({ status: 401 })
    expect(fetchSpy).toHaveBeenCalledTimes(1)
    expect(authorization(0)).toBe('Bearer old-token')
    expect(navigateWithFullReload).not.toHaveBeenCalled()
  })
})

/**
 * Holds the refresh path to its rate-limit pause: after a refresh is refused
 * with 429, no refresh is sent until the wait it named has passed, because the
 * refresh limit is shared by everyone behind one address.
 */
describe('getFreshAuthToken after a rate-limited refresh', () => {
  // After the stored token's expiry, so every call finds it due for a refresh
  const NOW = Date.parse('2030-01-01T00:00:00.000Z')

  it('sends no refresh until the wait a 429 named has passed', async () => {
    // Arrange — a fresh copy of the module, so the pause it starts cannot
    // reach any other test.
    vi.resetModules()
    const { getFreshAuthToken } = await import('./api-fetch')
    storeSession('2000-01-01T00:00:00.000Z')
    const now = vi.spyOn(Date, 'now').mockReturnValue(NOW)
    fetchSpy.mockImplementation(async () =>
      json({ detail: 'Slow down.' }, 429, { 'Retry-After': '30' }),
    )
    await getFreshAuthToken()

    // Act
    const whilePaused = await getFreshAuthToken()
    now.mockReturnValue(NOW + 29_999)
    await getFreshAuthToken()
    const callsWhilePaused = fetchSpy.mock.calls.length
    now.mockReturnValue(NOW + 30_000)
    await getFreshAuthToken()

    // Assert
    expect(callsWhilePaused).toBe(1)
    expect(fetchSpy).toHaveBeenCalledTimes(2)
    expect(whilePaused).toBe('old-token')
  })
})
