'use client'

import '@/styles/globals.css'
import React from 'react'
import { Provider } from 'react-redux'
import { Inter } from 'next/font/google'
import { store } from '../store'
import { AntdRegistry } from '@ant-design/nextjs-registry'
import ReleaseCleanup from '../components/release-cleanup'
import BreakpointIndicator from './_components/breakpoint-indicator'
import AuthGate from './_components/auth-gate'

const inter = Inter({ subsets: ['latin'] })

const SsrFallback = () => null

const subscribeNever = () => () => {}
const getClientSnapshot = () => true
const getServerSnapshot = () => false

const RootLayout = ({ children }: React.PropsWithChildren) => {
  // AuthGate reads localStorage (client-only) to decide what to render, so
  // the server and the hydrating render both produce a blank shell; React
  // re-renders with the client snapshot once hydration completes.
  const mounted = React.useSyncExternalStore(
    subscribeNever,
    getClientSnapshot,
    getServerSnapshot,
  )

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
