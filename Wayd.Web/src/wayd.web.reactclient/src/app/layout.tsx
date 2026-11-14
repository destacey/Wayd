import type React from 'react'
import { connection } from 'next/server'
import AppShell from './_components/app-shell'

/**
 * The root layout. It waits for the request so every page renders per request:
 * Next.js puts the request's Content-Security-Policy nonce on the scripts it
 * renders, and a prerendered page would carry none, so the policy would block it.
 */
export default async function RootLayout({ children }: React.PropsWithChildren) {
  await connection()
  return <AppShell>{children}</AppShell>
}
