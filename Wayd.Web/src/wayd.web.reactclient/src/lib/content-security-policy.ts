/** What the Content-Security-Policy for one page response depends on. */
export interface ContentSecurityPolicyOptions {
  /** The request's nonce. Next.js puts it on every script it renders. */
  nonce: string
  /** The API's base URL, `NEXT_PUBLIC_API_BASE_URL`. */
  apiBaseUrl: string | undefined
  /** The requested path. */
  pathname: string
  /** Whether the dev server is answering; React needs `eval` there. */
  isDevelopment: boolean
}

/**
 * The page that signs a user in and out of an identity provider. oidc-client-ts reaches the provider
 * from the browser (discovery, token and end-session endpoints), and providers are configured at run
 * time, so this page alone may connect to any https origin. It renders no user content.
 */
const PROVIDER_PAGE = '/login'

/**
 * The page that grants another application access to Wayd. No site may frame it, its own included,
 * so its Approve button cannot be overlaid.
 */
const CONSENT_PAGE = '/oauth/consent'

/** The API's origin and its WebSocket twin, for the SignalR hubs; none when the URL does not parse. */
function apiOrigins(apiBaseUrl: string | undefined): string[] {
  if (!apiBaseUrl) return []
  try {
    const { origin, protocol, host } = new URL(apiBaseUrl)
    if (protocol !== 'https:' && protocol !== 'http:') return []
    return [origin, `${protocol === 'https:' ? 'wss' : 'ws'}://${host}`]
  } catch {
    return []
  }
}

/**
 * Builds the Content-Security-Policy for a page response.
 *
 * Scripts run only with the request's nonce, and `'strict-dynamic'` extends that to the chunks they
 * load, so an injected script never runs. Styles allow inline: antd and the chart and editor
 * libraries write `<style>` elements and `style` attributes at run time. Images may come from any
 * https origin, because user markdown links remote images.
 */
export function contentSecurityPolicy({
  nonce,
  apiBaseUrl,
  pathname,
  isDevelopment,
}: ContentSecurityPolicyOptions): string {
  const api = apiOrigins(apiBaseUrl)
  const connect = ["'self'", ...api]
  if (pathname === PROVIDER_PAGE) connect.push('https:')

  const directives: Record<string, string[]> = {
    'default-src': ["'self'"],
    'script-src': [
      "'self'",
      `'nonce-${nonce}'`,
      "'strict-dynamic'",
      ...(isDevelopment ? ["'unsafe-eval'"] : []),
    ],
    'style-src': ["'self'", "'unsafe-inline'"],
    'img-src': ["'self'", 'data:', 'blob:', 'https:', ...api.slice(0, 1)],
    'font-src': ["'self'", 'data:'],
    'connect-src': connect,
    'worker-src': ["'self'", 'blob:'],
    'object-src': ["'none'"],
    'base-uri': ["'self'"],
    'form-action': ["'self'"],
    'frame-ancestors': [pathname === CONSENT_PAGE ? "'none'" : "'self'"],
  }

  return Object.entries(directives)
    .map(([name, sources]) => `${name} ${sources.join(' ')}`)
    .join('; ')
}

/** The `X-Frame-Options` that matches the policy's `frame-ancestors`, for browsers that ignore it. */
export function frameOptions(pathname: string): 'DENY' | 'SAMEORIGIN' {
  return pathname === CONSENT_PAGE ? 'DENY' : 'SAMEORIGIN'
}
