import {
  contentSecurityPolicy,
  frameOptions,
  type ContentSecurityPolicyOptions,
} from './content-security-policy'

function directives(options: Partial<ContentSecurityPolicyOptions> = {}) {
  const policy = contentSecurityPolicy({
    nonce: 'abc123',
    apiBaseUrl: 'https://api.wayd.example',
    pathname: '/work/sprints',
    isDevelopment: false,
    ...options,
  })
  return new Map(
    policy.split('; ').map((directive) => {
      const [name, ...sources] = directive.split(' ')
      return [name, sources] as const
    }),
  )
}

describe('contentSecurityPolicy', () => {
  it('runs only scripts that carry the request nonce', () => {
    // Act
    const policy = directives()

    // Assert
    expect(policy.get('script-src')).toEqual([
      "'self'",
      "'nonce-abc123'",
      "'strict-dynamic'",
    ])
    expect(policy.get('object-src')).toEqual(["'none'"])
    expect(policy.get('base-uri')).toEqual(["'self'"])
  })

  it('allows eval only on the dev server', () => {
    // Act
    const policy = directives({ isDevelopment: true })

    // Assert
    expect(policy.get('script-src')).toContain("'unsafe-eval'")
  })

  it('connects only to the client and the API, including its hubs', () => {
    // Act
    const policy = directives()

    // Assert
    expect(policy.get('connect-src')).toEqual([
      "'self'",
      'https://api.wayd.example',
      'wss://api.wayd.example',
    ])
  })

  it('lets the sign-in page reach an identity provider', () => {
    // Act
    const policy = directives({ pathname: '/login' })

    // Assert
    expect(policy.get('connect-src')).toContain('https:')
  })

  it('names no API when its URL does not parse', () => {
    // Act
    const policy = directives({ apiBaseUrl: 'APP_NEXT_PUBLIC_API_BASE_URL' })

    // Assert
    expect(policy.get('connect-src')).toEqual(["'self'"])
  })

  it.each([
    ['/oauth/consent', "'none'"],
    ['/login', "'self'"],
  ])('lets %s be framed by %s', (pathname, ancestor) => {
    // Act
    const policy = directives({ pathname })

    // Assert
    expect(policy.get('frame-ancestors')).toEqual([ancestor])
  })
})

describe('frameOptions', () => {
  it.each([
    ['/oauth/consent', 'DENY'],
    ['/', 'SAMEORIGIN'],
  ])('answers %s with %s', (pathname, expected) => {
    // Act
    const value = frameOptions(pathname)

    // Assert
    expect(value).toBe(expected)
  })
})
