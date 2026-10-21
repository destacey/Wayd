/**
 * The page a signed-out visitor asked for, kept across sign-in so they land back
 * on it. It lives in sessionStorage, so it stays with the tab and survives the
 * round trip to an identity provider.
 */
const RETURN_URL_KEY = 'wayd.returnUrl'

/** Paths that are part of signing in or out, never somewhere to return to. */
const EXCLUDED_PATHS = new Set(['/', '/login', '/logout', '/setup'])

/**
 * The path and query of `url` when it is a same-origin, relative location;
 * otherwise null. The value is read back from storage and navigated to, so it
 * is resolved with the browser's own URL parser and only the resolved path is
 * returned: the forms that parser reads as another host (`//host`, `/\host`,
 * a tab inside `/\t/host`) fail the origin check rather than slipping past it.
 * Dot segments can still resolve to a path that begins `//` (`/.//host`), which
 * the browser reads as another host when navigated to, so that is refused too.
 */
export const toSafeReturnPath = (url: string | null): string | null => {
  if (!url || !url.startsWith('/')) return null

  try {
    const origin = window.location.origin
    const resolved = new URL(url, origin)
    if (resolved.origin !== origin) return null
    if (resolved.pathname.startsWith('//')) return null
    if (EXCLUDED_PATHS.has(resolved.pathname)) return null
    return resolved.pathname + resolved.search
  } catch {
    return null
  }
}

/** Remembers where the visitor is now — path and query — to return to after signing in. */
export const storeReturnUrl = (): void => {
  if (typeof window === 'undefined') return
  const { pathname, search } = window.location
  const path = toSafeReturnPath(pathname + search)
  if (path) {
    sessionStorage.setItem(RETURN_URL_KEY, path)
  }
}

/** Reads and clears the remembered location; null when there is none or it is not safe to follow. */
export const takeReturnUrl = (): string | null => {
  if (typeof window === 'undefined') return null
  const stored = sessionStorage.getItem(RETURN_URL_KEY)
  if (stored === null) return null
  sessionStorage.removeItem(RETURN_URL_KEY)
  return toSafeReturnPath(stored)
}
