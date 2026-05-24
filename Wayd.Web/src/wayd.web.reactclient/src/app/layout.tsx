'use client'

import '@/styles/globals.css'
import React, {
  memo,
  PropsWithChildren,
  useEffect,
  useSyncExternalStore,
} from 'react'
import { Provider } from 'react-redux'
import { Inter } from 'next/font/google'
import { App, Grid, Layout } from 'antd'
import { store } from '../store'
import AppHeader from './_components/app-header'
import AppSideNav from './_components/menu/app-side-nav'
import AppBreadcrumb from './_components/app-breadcrumb'
import { ThemeProvider } from '../components/contexts/theme'
import useTheme from '../components/contexts/theme'
import { MenuToggleProvider } from '../components/contexts/menu-toggle'
import { AntdRegistry } from '@ant-design/nextjs-registry'
import { AuthProvider } from '../components/contexts/auth'
import { MessageProvider } from '../components/contexts/messaging'
import LoginPage from './login/page'
import LogoutPage from './logout/page'
import SetupPage from './setup/page'
import { usePathname, useRouter } from 'next/navigation'
import { isAuthActive } from '../services/clients'
import ReleaseCleanup from '../components/release-cleanup'
import BreakpointIndicator from './_components/breakpoint-indicator'

const { Content } = Layout
const { useBreakpoint } = Grid

const inter = Inter({ subsets: ['latin'] })

const RETURN_URL_KEY = 'wayd.returnUrl'

const AppContent = memo(({ children }: PropsWithChildren) => {
  const screens = useBreakpoint()
  const isMobile = !screens.md
  const router = useRouter()
  const { currentThemeName } = useTheme()

  // After authentication, redirect to the originally requested URL if one was stored.
  useEffect(() => {
    const returnUrl = sessionStorage.getItem(RETURN_URL_KEY)
    if (returnUrl) {
      sessionStorage.removeItem(RETURN_URL_KEY)
      router.replace(returnUrl)
    }
  }, [router])

  return (
    <Layout className={`app-theme-${currentThemeName}`}>
      <AppHeader />
      <Layout hasSider className="app-main-layout">
        <AppSideNav isMobile={isMobile} />
        <Content className="app-main-content">
          <AppBreadcrumb />
          {children}
        </Content>
      </Layout>
    </Layout>
  )
})

AppContent.displayName = 'AppContent'

const AppProviders = ({ children }: PropsWithChildren) => (
  <ThemeProvider>
    <MenuToggleProvider>
      <App>
        <MessageProvider>{children}</MessageProvider>
      </App>
    </MenuToggleProvider>
  </ThemeProvider>
)

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
          <AppContent>{children}</AppContent>
        </AuthProvider>
      </AppProviders>
    )
  }

  // Preserve the intended URL so login can redirect back after authentication.
  if (
    typeof window !== 'undefined' &&
    pathname &&
    pathname !== '/' &&
    pathname !== '/login' &&
    pathname !== '/logout' &&
    pathname !== '/setup'
  ) {
    sessionStorage.setItem(RETURN_URL_KEY, pathname)
  }

  if (pathname === '/logout') {
    return <LogoutPage />
  }

  if (pathname === '/setup') {
    return <SetupPage />
  }

  return <LoginPage />
}

const SsrFallback = () => null

const RootLayout = ({ children }: React.PropsWithChildren) => {
  // Suppress the server/client hydration mismatch that arises because
  // AuthGate reads localStorage (client-only) to decide what to render.
  // The server always produces a blank shell; we swap in the real tree
  // after the first client render, keeping server and first-paint HTML
  // identical.
  const [mounted, setMounted] = React.useState(false)
  React.useEffect(() => { setMounted(true) }, [])

  return (
    <html lang="en">
      <head>
        <meta
          name="viewport"
          content="width=device-width, initial-scale=1, viewport-fit=cover"
        />
        <link rel="apple-touch-icon" href="/wayd-icon.png" />
        <meta name="mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-status-bar-style" content="default" />
      </head>
      <body className={inter.className}>
        <ReleaseCleanup />
        {mounted ? (
          <AntdRegistry>
            <Provider store={store}>
              <AuthGate>{children}</AuthGate>
            </Provider>
            {process.env.NODE_ENV !== 'production' && <BreakpointIndicator />}
          </AntdRegistry>
        ) : (
          <SsrFallback />
        )}
      </body>
    </html>
  )
}

export default RootLayout
