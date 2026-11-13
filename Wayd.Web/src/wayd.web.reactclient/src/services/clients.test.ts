// @vitest-environment jsdom
import { authenticatedFetch, clearAuth, getFreshAuthToken } from './clients'
import { ApiException, AuthClient } from './wayd-api'

/**
 * Holds the refresh path to its rate-limit pause: after a refresh is refused
 * with 429, no refresh is sent until the wait it named has passed, because the
 * refresh limit is shared by everyone behind one address.
 */
describe('getFreshAuthToken after a rate-limited refresh', () => {
  // After the stored token's expiry, so every call finds it due for a refresh
  const NOW = Date.parse('2030-01-01T00:00:00.000Z')

  beforeEach(() => {
    localStorage.setItem('wayd.local.token', 'expired-token')
    localStorage.setItem('wayd.local.refreshToken', 'refresh-token')
    localStorage.setItem('wayd.local.tokenExpiry', '2000-01-01T00:00:00.000Z')
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('sends no refresh until the wait a 429 named has passed', async () => {
    // Arrange
    const now = vi.spyOn(Date, 'now').mockReturnValue(NOW)
    const refresh = vi
      .spyOn(AuthClient.prototype, 'refreshToken')
      .mockRejectedValue(
        new ApiException(
          'Too Many Requests',
          429,
          '',
          { 'retry-after': '30' },
          null,
        ),
      )
    await getFreshAuthToken()

    // Act
    const whilePaused = await getFreshAuthToken()
    now.mockReturnValue(NOW + 29_999)
    await getFreshAuthToken()
    const callsWhilePaused = refresh.mock.calls.length
    now.mockReturnValue(NOW + 30_000)
    await getFreshAuthToken()

    // Assert
    expect(callsWhilePaused).toBe(1)
    expect(refresh).toHaveBeenCalledTimes(2)
    expect(whilePaused).toBe('expired-token')
  })
})

describe('clearAuth', () => {
  it('removes the identity provider redirect state from both storages', () => {
    // Arrange
    localStorage.setItem('oidc.abandoned-state', '{"code_verifier":"v"}')
    sessionStorage.setItem('oidc.user:https://idp.example:client', '{}')
    localStorage.setItem('wayd.theme', 'dark')

    // Act
    clearAuth()

    // Assert
    expect(localStorage.getItem('oidc.abandoned-state')).toBeNull()
    expect(
      sessionStorage.getItem('oidc.user:https://idp.example:client'),
    ).toBeNull()
    expect(localStorage.getItem('wayd.theme')).toBe('dark')
  })
})

describe('authenticatedFetch', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    localStorage.clear()
  })

  it.each([
    'https://attacker.example/collect',
    '//attacker.example/collect',
    'api/user-management/users',
  ])('refuses %s without sending the token', async (target) => {
    // Arrange
    localStorage.setItem('wayd.local.token', 'secret-token')
    const fetchSpy = vi.spyOn(globalThis, 'fetch')

    // Act
    const call = authenticatedFetch(target)

    // Assert
    await expect(call).rejects.toThrow('takes an API path')
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('sends an API path with the bearer token', async () => {
    // Arrange
    localStorage.setItem('wayd.local.token', 'secret-token')
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(null, { status: 204 }))

    // Act
    await authenticatedFetch('/api/user-management/users/1')

    // Assert
    const [url, init] = fetchSpy.mock.calls[0]
    expect(String(url).endsWith('/api/user-management/users/1')).toBe(true)
    expect(new Headers(init?.headers).get('Authorization')).toBe(
      'Bearer secret-token',
    )
  })
})
