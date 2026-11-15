// @vitest-environment jsdom
import { authenticatedFetch } from './clients'

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
