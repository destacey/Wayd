'use client'

import { memo, PropsWithChildren, useEffect, useSyncExternalStore } from 'react'
import { App, Grid, Layout } from 'antd'
import { usePathname, useRouter } from 'next/navigation'
import AppHeader from './app-header'
import AppSideNav from './menu/app-side-nav'
import { ThemeProvider } from '../../components/contexts/theme'
import useTheme from '../../components/contexts/theme'
import { MenuToggleProvider } from '../../components/contexts/menu-toggle'
import { AuthProvider } from '../../components/contexts/auth'
import { MessageProvider } from '../../components/contexts/messaging'
import LoginPage from '../login/page'
import LogoutPage from '../logout/page'
import SetupPage from '../setup/page'
import { isAuthActive } from '../../services/clients'
import { storeReturnUrl, takeReturnUrl } from '../../utils/return-url'

const { Content } = Layout
const { useBreakpoint } = Grid

/** Pages that run without the header and side nav, for a signed-in user too. */
const SHELL_FREE_PATHS = new Set(['/oauth/consent'])

const AppContent = memo(({ children }: PropsWithChildren) => {
  const screens = useBreakpoint()
  const isMobile = !screens.md
  const router = useRouter()
  const { currentTheme } = useTheme()

  // Sign-in navigates to the stored URL itself; this catches a session that
  // arrived another way, such as a sign-in in another tab.
  useEffect(() => {
    const returnUrl = takeReturnUrl()
    if (returnUrl) {
      router.replace(returnUrl)
    }
  }, [router])

  return (
    <Layout className={`app-theme-${currentTheme}`}>
      <AppHeader />
      <Layout hasSider className="app-main-layout">
        <AppSideNav isMobile={isMobile} />
        {/* A scroll container and nothing more. Pages that want gutters opt in
            with `.page-gutters`; record pages and the full-viewport auth pages
            run edge-to-edge. Padding here would inset both, and a child cannot
            reliably un-pad its parent. */}
        <Content className="app-main-content">{children}</Content>
      </Layout>
    </Layout>
  )
})

AppContent.displayName = 'AppContent'

/**
 * A page without the app shell. Reached while signed in, it has no AppContent
 * to consume the stored return URL, so it drops it: left behind, the URL would
 * send the next visit to any shell page back here, after it has gone stale.
 */
const ShellFreeContent = ({ children }: PropsWithChildren) => {
  useEffect(() => {
    takeReturnUrl()
  }, [])

  return children
}

const AppProviders = ({ children }: PropsWithChildren) => (
  <ThemeProvider>
    <MenuToggleProvider>
      <App>
        <MessageProvider>{children}</MessageProvider>
      </App>
    </MenuToggleProvider>
  </ThemeProvider>
)

// Re-check auth state whenever another tab writes or clears the Wayd JWT.
// Without this, a login in Tab A leaves Tab B stuck on /login until refresh.
const subscribeAuthStorage = (callback: () => void) => {
  const handler = (event: StorageEvent) => {
    if (event.key === null || event.key.startsWith('wayd.local.')) {
      callback()
    }
  }
  window.addEventListener('storage', handler, { passive: true })
  return () => window.removeEventListener('storage', handler)
}
const getAuthSnapshot = () => isAuthActive()
const getAuthServerSnapshot = () => false

/**
 * Auth gate — single-check on whether a Wayd JWT is in storage. If yes,
 * render the app; otherwise render the login page (or logout on /logout).
 *
 * MSAL state is deliberately not part of the gate decision. Post-PR 3.2 the
 * Wayd JWT *is* the session; MSAL is just the mechanism the login page uses
 * to acquire an initial token via /api/auth/exchange. Treating MSAL state as
 * part of the gate would conflate "has an Entra session cookie" with "logged
 * in to Wayd" — the source of the logout re-login race that PR 3.2's earlier
 * revisions hit.
 */
const AuthGate = ({ children }: PropsWithChildren) => {
  const pathname = usePathname()
  const auth = useSyncExternalStore(
    subscribeAuthStorage,
    getAuthSnapshot,
    getAuthServerSnapshot,
  )

  if (auth) {
    return (
      <AppProviders>
        <AuthProvider>
          {pathname && SHELL_FREE_PATHS.has(pathname) ? (
            <ShellFreeContent>{children}</ShellFreeContent>
          ) : (
            <AppContent>{children}</AppContent>
          )}
        </AuthProvider>
      </AppProviders>
    )
  }

  // Preserve the intended URL so login can redirect back after authentication.
  storeReturnUrl()

  if (pathname === '/logout') {
    return <LogoutPage />
  }

  if (pathname === '/setup') {
    return <SetupPage />
  }

  return <LoginPage />
}

export default AuthGate
