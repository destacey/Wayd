import { NextResponse, type NextRequest } from 'next/server'
import {
  contentSecurityPolicy,
  frameOptions,
} from '@/src/lib/content-security-policy'

/**
 * Gives every page a fresh nonce and the Content-Security-Policy that names it. Next.js reads the
 * policy off the request and puts the nonce on the scripts it renders, which is why the root layout
 * renders per request: a prerendered page has no nonce and the policy would block its scripts.
 */
export function proxy(request: NextRequest) {
  const nonce = btoa(crypto.randomUUID())
  const { pathname } = request.nextUrl
  const policy = contentSecurityPolicy({
    nonce,
    apiBaseUrl: process.env.NEXT_PUBLIC_API_BASE_URL,
    pathname,
    isDevelopment: process.env.NODE_ENV === 'development',
  })

  const requestHeaders = new Headers(request.headers)
  requestHeaders.set('Content-Security-Policy', policy)

  const response = NextResponse.next({ request: { headers: requestHeaders } })
  response.headers.set('Content-Security-Policy', policy)
  response.headers.set('X-Frame-Options', frameOptions(pathname))
  return response
}

export const config = {
  matcher: [
    {
      // Pages only: route handlers, build assets, the service worker and public files carry no
      // scripts for a nonce to cover.
      source:
        '/((?!api/|_next/static|_next/image|serwist/|favicon\\.ico|icons/|images/|screenshots/|.*\\.(?:png|svg|ico|json|webmanifest|txt|xml)$).*)',
      // A prefetch renders no document, so it needs no policy.
      missing: [
        { type: 'header', key: 'next-router-prefetch' },
        { type: 'header', key: 'purpose', value: 'prefetch' },
      ],
    },
  ],
}
