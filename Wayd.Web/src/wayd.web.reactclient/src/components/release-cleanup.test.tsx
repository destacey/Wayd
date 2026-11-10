import type { Mock } from 'vitest'
import { render, waitFor } from '@testing-library/react'
import ReleaseCleanup from './release-cleanup'

const RELEASE_MARKER_KEY = 'wayd.release.cleanup'
const RELEASE_MARKER_VALUE = 'pr-3.2.2'

// --- Mocks for browser APIs not in jsdom ---

function mockServiceWorker(registrations: Array<{ unregister: Mock }> = []) {
  Object.defineProperty(navigator, 'serviceWorker', {
    configurable: true,
    value: {
      getRegistrations: vi.fn().mockResolvedValue(registrations),
    },
  })
}

function removeServiceWorker() {
  delete (navigator as unknown as { serviceWorker?: unknown }).serviceWorker
}

function mockCaches(keys: string[] = []) {
  const deleteFn = vi.fn().mockResolvedValue(true)
  Object.defineProperty(window, 'caches', {
    configurable: true,
    value: {
      keys: vi.fn().mockResolvedValue(keys),
      delete: deleteFn,
    },
  })
  return deleteFn
}

function removeCaches() {
  delete (window as unknown as { caches?: unknown }).caches
}

beforeEach(() => {
  vi.clearAllMocks()
})

afterEach(() => {
  removeServiceWorker()
  removeCaches()
})

describe('ReleaseCleanup', () => {
  it('unregisters service workers, clears caches, and reloads once on first load', async () => {
    const reload = vi.fn()
    const swUnregister = vi.fn().mockResolvedValue(true)
    mockServiceWorker([{ unregister: swUnregister }])
    const cacheDelete = mockCaches(['old-cache-1', 'old-cache-2'])

    render(<ReleaseCleanup onReload={reload} />)

    await waitFor(() => expect(reload).toHaveBeenCalled())

    expect(swUnregister).toHaveBeenCalledTimes(1)
    expect(cacheDelete).toHaveBeenCalledWith('old-cache-1')
    expect(cacheDelete).toHaveBeenCalledWith('old-cache-2')
    expect(localStorage.getItem(RELEASE_MARKER_KEY)).toBe(RELEASE_MARKER_VALUE)
  })

  it('removes legacy moda.local.* storage entries', async () => {
    const reload = vi.fn()
    mockServiceWorker()
    mockCaches()
    localStorage.setItem('moda.local.token', 'stale-token')
    localStorage.setItem('moda.local.refreshToken', 'stale-refresh')
    sessionStorage.setItem('moda.local.tokenExpiry', 'stale-expiry')

    render(<ReleaseCleanup onReload={reload} />)

    await waitFor(() => expect(reload).toHaveBeenCalled())

    expect(localStorage.getItem('moda.local.token')).toBeNull()
    expect(localStorage.getItem('moda.local.refreshToken')).toBeNull()
    expect(sessionStorage.getItem('moda.local.tokenExpiry')).toBeNull()
  })

  it('migrates modaTheme → appTheme, preserving the user preference', async () => {
    const reload = vi.fn()
    mockServiceWorker()
    mockCaches()
    localStorage.setItem('modaTheme', '"dark"')

    render(<ReleaseCleanup onReload={reload} />)

    await waitFor(() => expect(reload).toHaveBeenCalled())

    expect(localStorage.getItem('modaTheme')).toBeNull()
    expect(localStorage.getItem('appTheme')).toBe('"dark"')
  })

  it('migrates modaMenuCollapsed → appMenuCollapsed, preserving the user preference', async () => {
    const reload = vi.fn()
    mockServiceWorker()
    mockCaches()
    localStorage.setItem('modaMenuCollapsed', 'false')

    render(<ReleaseCleanup onReload={reload} />)

    await waitFor(() => expect(reload).toHaveBeenCalled())

    expect(localStorage.getItem('modaMenuCollapsed')).toBeNull()
    expect(localStorage.getItem('appMenuCollapsed')).toBe('false')
  })

  it('does not overwrite an already-populated destination key during migration', async () => {
    // Edge case: another device / tab already seeded the new key.
    // Preserve the new value, drop the old one.
    const reload = vi.fn()
    mockServiceWorker()
    mockCaches()
    localStorage.setItem('modaTheme', '"light"') // stale old value
    localStorage.setItem('appTheme', '"dark"') // fresh new value

    render(<ReleaseCleanup onReload={reload} />)

    await waitFor(() => expect(reload).toHaveBeenCalled())

    expect(localStorage.getItem('modaTheme')).toBeNull()
    expect(localStorage.getItem('appTheme')).toBe('"dark"')
  })

  it('leaves the destination key alone when the source key is absent', async () => {
    const reload = vi.fn()
    mockServiceWorker()
    mockCaches()
    // No modaTheme; user has never touched the theme on this browser.

    render(<ReleaseCleanup onReload={reload} />)

    await waitFor(() => expect(reload).toHaveBeenCalled())

    expect(localStorage.getItem('appTheme')).toBeNull()
  })

  it('is a no-op when the marker is already set (already cleaned this release)', async () => {
    localStorage.setItem(RELEASE_MARKER_KEY, RELEASE_MARKER_VALUE)
    const reload = vi.fn()
    const getRegistrations = vi.fn().mockResolvedValue([])
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: { getRegistrations },
    })

    render(<ReleaseCleanup onReload={reload} />)

    // Give any microtasks a tick to settle, but nothing should have fired.
    await new Promise((r) => setTimeout(r, 10))

    expect(reload).not.toHaveBeenCalled()
    expect(getRegistrations).not.toHaveBeenCalled()
  })

  it('still marks cleanup done and reloads if service worker APIs are unavailable', async () => {
    // No navigator.serviceWorker, no window.caches — older browsers, or SSR-
    // initialized environments that never got browser-only globals. The
    // cleanup should still mark itself complete and reload so the user isn't
    // permanently stuck.
    const reload = vi.fn()

    render(<ReleaseCleanup onReload={reload} />)

    await waitFor(() => expect(reload).toHaveBeenCalled())
    expect(localStorage.getItem(RELEASE_MARKER_KEY)).toBe(RELEASE_MARKER_VALUE)
  })

  it('continues cleanup when one service worker unregister rejects (uses allSettled)', async () => {
    // Rationale: if SW unregister throws and aborts the cleanup pipeline, the
    // user still gets the marker written (finally block) and the reload — but
    // none of the migrations/deletions after the SW step ran, and now the
    // marker prevents a retry. allSettled keeps the pipeline going so the
    // downstream steps (caches, migrations, legacy deletions) complete.
    const reload = vi.fn()
    const failingUnregister = vi.fn().mockRejectedValue(new Error('sw failure'))
    const succeedingUnregister = vi.fn().mockResolvedValue(true)
    mockServiceWorker([
      { unregister: failingUnregister },
      { unregister: succeedingUnregister },
    ])
    mockCaches(['old-cache'])
    localStorage.setItem('modaTheme', '"dark"')

    render(<ReleaseCleanup onReload={reload} />)

    await waitFor(() => expect(reload).toHaveBeenCalled())

    // Both unregisters were attempted — the failing one didn't short-circuit.
    expect(failingUnregister).toHaveBeenCalledTimes(1)
    expect(succeedingUnregister).toHaveBeenCalledTimes(1)
    // Downstream cleanup also ran: migration moved theme, marker is set.
    expect(localStorage.getItem('modaTheme')).toBeNull()
    expect(localStorage.getItem('appTheme')).toBe('"dark"')
    expect(localStorage.getItem(RELEASE_MARKER_KEY)).toBe(RELEASE_MARKER_VALUE)
  })
})
