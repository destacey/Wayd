'use client'

import { useEffect } from 'react'

/**
 * One-shot cleanup for browsers carrying pre-PR-3.2 state.
 *
 * Without this, returning users are stuck in a loop: the old service worker
 * serves the cached old app shell, which tries to hit `moda-api.*`/permissions
 * (an endpoint removed in PR 3.2) with MSAL-direct-to-API tokens, gets 401,
 * retries forever. Clearing site data manually works; users shouldn't have to.
 *
 * On first load per browser after this ships:
 *   1. Unregister every service worker (nukes the stale cached app shell).
 *   2. Delete every Cache Storage entry (nukes stale chunks + runtime-cached
 *      API responses the old SW populated).
 *   3. Migrate any renamed localStorage keys (copy value to new key), then
 *      drop the old entries — preserves user preferences across renames.
 *   4. Remove pre-rename storage entries that we're not preserving (auth
 *      tokens, etc. — they need to be reacquired anyway).
 *   5. Mark the cleanup done (localStorage flag) and hard-reload so the fresh
 *      app boots without the old SW intercepting.
 *
 * Gated by a per-release marker so it runs at most once per browser per
 * release. Subsequent releases bump `RELEASE_MARKER_VALUE` to force another
 * pass; the marker key itself stays stable so we don't leak flags.
 */
const RELEASE_MARKER_KEY = 'wayd.release.cleanup'
// Bump this value whenever the legacy-key list or the required-cleanup scope
// changes — a new value re-runs the cleanup once per browser without leaking
// a separate marker per release.
const RELEASE_MARKER_VALUE = 'pr-3.2.2'

/**
 * Storage keys to migrate. For each entry, if `from` exists in localStorage,
 * its raw string value is copied to `to` (if `to` doesn't already exist), and
 * `from` is removed either way. Values are copied verbatim so
 * `useLocalStorageState`'s JSON shape is preserved across the rename.
 */
const STORAGE_KEY_MIGRATIONS: Array<{ from: string; to: string }> = [
  { from: 'modaTheme', to: 'appTheme' },
  { from: 'modaMenuCollapsed', to: 'appMenuCollapsed' },
]

/**
 * Storage keys to delete outright. Auth-related entries from the Moda era —
 * they're invalid under the new identity model anyway, so preserving them
 * would just leak stale sessions.
 */
const LEGACY_STORAGE_KEYS = [
  'moda.local.token',
  'moda.local.refreshToken',
  'moda.local.tokenExpiry',
  'moda.local.mustChangePassword',
  'moda.local.rememberMe',
]

interface ReleaseCleanupProps {
  /**
   * Injection point for tests. Production path always uses `window.location.reload`;
   * tests pass a vi.fn so the test environment doesn't need to mock jsdom's
   * non-configurable `location`.
   */
  onReload?: () => void
}

export default function ReleaseCleanup({ onReload }: ReleaseCleanupProps = {}) {
  useEffect(() => {
    if (typeof window === 'undefined') return
    // The marker flip happens before the reload, so any remount — StrictMode
    // double-invoke in dev, or a revisit later in the same tab — is a no-op.
    if (localStorage.getItem(RELEASE_MARKER_KEY) === RELEASE_MARKER_VALUE)
      return

    const run = async () => {
      try {
        if ('serviceWorker' in navigator) {
          const registrations = await navigator.serviceWorker.getRegistrations()
          // allSettled, not all: if one registration's unregister() rejects,
          // we still want to try the others AND keep going through caches +
          // storage migrations below. Otherwise a single failed SW leaves the
          // user half-cleaned but marked-done, with no retry path.
          await Promise.allSettled(registrations.map((r) => r.unregister()))
        }
        if ('caches' in window) {
          const keys = await caches.keys()
          await Promise.allSettled(keys.map((k) => caches.delete(k)))
        }
        for (const { from, to } of STORAGE_KEY_MIGRATIONS) {
          const existing = localStorage.getItem(from)
          if (existing !== null) {
            // Only write if the destination is empty — don't overwrite a
            // value the user already set against the new key (e.g., someone
            // who logged in on one device, set theme to dark, and now the
            // cleanup fires on a different device with an older `from` value).
            if (localStorage.getItem(to) === null) {
              localStorage.setItem(to, existing)
            }
            localStorage.removeItem(from)
          }
        }
        for (const key of LEGACY_STORAGE_KEYS) {
          localStorage.removeItem(key)
          sessionStorage.removeItem(key)
        }
      } catch (e) {
        console.warn(
          '[ReleaseCleanup] cleanup failed, continuing so the user is not stuck retrying forever.',
          e,
        )
      } finally {
        localStorage.setItem(RELEASE_MARKER_KEY, RELEASE_MARKER_VALUE)
        // Full reload so the new SW (if any) registers clean and the app
        // boots without the old bundle's handlers still subscribed.
        if (onReload) {
          onReload()
        } else {
          window.location.reload()
        }
      }
    }

    run()
  }, [onReload])

  return null
}
