'use client'

import { useEffect, useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import styles from './page.module.css'
import {
  getAuthClient,
  isAuthActive,
  setRememberMe as persistRememberMe,
  storeAuth,
} from '@/src/services/clients'
import { useGetAuthProvidersQuery } from '@/src/store/features/common/auth-providers-api'
import { LoadingAccount } from '@/src/components/common'
import { useDocumentTitle } from '@/src/hooks'
import { OidcProviderInfo, OidcProviderType } from '@/src/services/wayd-api'
import {
  clearChosenProvider,
  completeSignin,
  getChosenProviderName,
  getLastProviderName,
  signinRedirect,
  signinSilent,
} from '@/src/components/contexts/auth/oidc-client-registry'

const pulseAnimation = `
@keyframes pulse {
  0%, 100% { r: 4; opacity: 1; }
  50% { r: 6; opacity: 0.7; }
}
@keyframes fadeFlow {
  0% { stroke-dashoffset: 200; }
  100% { stroke-dashoffset: 0; }
}
@keyframes chartDraw {
  0% { stroke-dashoffset: 150; }
  100% { stroke-dashoffset: 0; }
}
@keyframes glowPulse {
  0%, 100% { opacity: 0.3; }
  50% { opacity: 0.7; }
}
`

function AnalyticsIllustration() {
  // Animations are CSS-based (keyframes), no React state needed
  const nodes = [
    { x: 140, y: 80, r: 10, color: '#fff', label: 'Teams' },
    { x: 60, y: 160, r: 7, color: '#90caf9', label: 'Sprints' },
    { x: 240, y: 140, r: 8, color: '#bbdefb', label: 'Dependencies' },
    { x: 180, y: 220, r: 6, color: '#fff', label: 'Work Items' },
    { x: 80, y: 280, r: 9, color: '#90caf9', label: 'PIs' },
    { x: 260, y: 260, r: 7, color: '#bbdefb', label: 'Projects' },
    { x: 140, y: 320, r: 5, color: '#fff', label: 'Objectives' },
    { x: 30, y: 100, r: 5, color: '#bbdefb', label: '' },
    { x: 300, y: 60, r: 4, color: '#90caf9', label: '' },
    { x: 320, y: 200, r: 5, color: '#fff', label: '' },
    { x: 20, y: 220, r: 4, color: '#90caf9', label: '' },
  ]

  const edges = [
    [0, 1], // Teams -> Sprints
    [0, 2], // Teams -> Dependencies
    [1, 3], // Sprints -> Work Items
    [2, 3], // Dependencies -> Work Items
    [1, 4], // Sprints -> PIs
    [4, 6], // PIs -> Objectives (solid)
    [3, 6], // Work Items -> Objectives
    [3, 5], // Work Items -> Projects
    [0, 7],
    [0, 8],
    [2, 5],
    [5, 9],
    [1, 10],
  ]

  return (
    <svg
      viewBox="0 0 340 400"
      style={{ width: '100%', height: '100%', maxHeight: 400 }}
    >
      <style>
        {pulseAnimation}
        {`
        .edge { stroke: rgba(100,181,246,0.35); stroke-width: 1.2; fill: none; stroke-dasharray: 4 6; }
        .edge-anim { stroke: rgba(144,202,249,0.6); stroke-width: 1.5; fill: none; stroke-dasharray: 200; stroke-dashoffset: 200; animation: fadeFlow 2.5s ease forwards; }
        .node-glow { animation: glowPulse 3s ease-in-out infinite; }
      `}
      </style>

      {/* Background gradients and filters */}
      <defs>
        <radialGradient id="glow1" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#2196f3" stopOpacity="0.25" />
          <stop offset="100%" stopColor="#2196f3" stopOpacity="0" />
        </radialGradient>
        <radialGradient id="glow2" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#64b5f6" stopOpacity="0.2" />
          <stop offset="100%" stopColor="#64b5f6" stopOpacity="0" />
        </radialGradient>
        <filter id="shadow">
          <feDropShadow
            dx="0"
            dy="2"
            stdDeviation="3"
            floodColor="#1565c0"
            floodOpacity="0.4"
          />
        </filter>
        <linearGradient id="bgGrad" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="#42a5f5" />
          <stop offset="25%" stopColor="#2196f3" />
          <stop offset="50%" stopColor="#1976d2" />
          <stop offset="75%" stopColor="#1565c0" />
          <stop offset="100%" stopColor="#0d47a1" />
        </linearGradient>
        <pattern
          id="gridPattern"
          width="36"
          height="36"
          patternUnits="userSpaceOnUse"
        >
          <path
            d="M 36 0 L 0 0 0 36"
            fill="none"
            stroke="rgba(255,255,255,0.07)"
            strokeWidth="1"
          />
        </pattern>
      </defs>

      <rect x="4" y="4" width="332" height="392" rx="8" fill="url(#bgGrad)" />
      <rect
        x="4"
        y="4"
        width="332"
        height="392"
        rx="8"
        fill="url(#gridPattern)"
      />

      <ellipse cx="100" cy="150" rx="90" ry="80" fill="url(#glow1)" />
      <ellipse cx="240" cy="250" rx="70" ry="70" fill="url(#glow2)" />

      {/* Edges */}
      {edges.map(([a, b], i) => (
        <line
          key={i}
          className={i < 6 ? 'edge-anim' : 'edge'}
          x1={nodes[a].x}
          y1={nodes[a].y}
          x2={nodes[b].x}
          y2={nodes[b].y}
          style={i < 6 ? { animationDelay: `${i * 0.3}s` } : {}}
        />
      ))}

      {/* Mini Bar Chart - top right */}
      <g transform="translate(248, 24)" filter="url(#shadow)">
        <rect
          x="0"
          y="0"
          width="52"
          height="42"
          rx="6"
          fill="rgba(13,71,161,0.7)"
          stroke="rgba(100,181,246,0.3)"
          strokeWidth="1"
        />
        {[
          { x: 6, h: 14, color: '#1976d2' },
          { x: 14, h: 22, color: '#42a5f5' },
          { x: 22, h: 10, color: '#90caf9' },
          { x: 30, h: 28, color: '#1976d2' },
          { x: 38, h: 18, color: '#42a5f5' },
        ].map((b, i) => (
          <rect
            key={i}
            x={b.x}
            y={38 - b.h}
            width="6"
            height={b.h}
            rx="2"
            fill={b.color}
            opacity="0.9"
          />
        ))}
      </g>

      {/* Mini Line Chart - left side */}
      <g transform="translate(10, 240)" filter="url(#shadow)">
        <rect
          x="0"
          y="0"
          width="56"
          height="40"
          rx="6"
          fill="rgba(13,71,161,0.7)"
          stroke="rgba(100,181,246,0.3)"
          strokeWidth="1"
        />
        <polyline
          points="4,32 12,24 20,28 30,14 40,18 50,8"
          fill="none"
          stroke="#42a5f5"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          style={{
            strokeDasharray: 150,
            strokeDashoffset: 150,
            animation: 'chartDraw 2s 1s ease forwards',
          }}
        />
        <circle cx="50" cy="8" r="2.5" fill="#bbdefb" />
      </g>

      {/* Mini Donut - bottom right */}
      <g transform="translate(272, 290)" filter="url(#shadow)">
        <rect
          x="-24"
          y="-24"
          width="48"
          height="48"
          rx="8"
          fill="rgba(13,71,161,0.7)"
          stroke="rgba(100,181,246,0.3)"
          strokeWidth="1"
        />
        <circle
          cx="0"
          cy="0"
          r="14"
          fill="none"
          stroke="#0d47a1"
          strokeWidth="5"
        />
        <circle
          cx="0"
          cy="0"
          r="14"
          fill="none"
          stroke="#1976d2"
          strokeWidth="5"
          strokeDasharray="52 36"
          strokeLinecap="round"
          transform="rotate(-90)"
        />
        <circle
          cx="0"
          cy="0"
          r="14"
          fill="none"
          stroke="#64b5f6"
          strokeWidth="5"
          strokeDasharray="22 66"
          strokeLinecap="round"
          transform="rotate(48)"
        />
      </g>

      {/* Nodes */}
      {nodes.map((n, i) => (
        <g key={i}>
          <circle
            cx={n.x}
            cy={n.y}
            r={n.r + 5}
            fill={n.color}
            opacity="0.08"
            className="node-glow"
            style={{ animationDelay: `${i * 0.4}s` }}
          />
          <circle
            cx={n.x}
            cy={n.y}
            r={n.r}
            fill={n.color}
            filter="url(#shadow)"
          />
          <circle cx={n.x} cy={n.y} r={n.r * 0.4} fill="rgba(13,71,161,0.6)" />
          {n.label && (
            <text
              x={n.x}
              y={n.y + n.r + 14}
              textAnchor="middle"
              fill="rgba(187,222,251,0.85)"
              fontSize="9"
              fontFamily="'Segoe UI', system-ui, sans-serif"
              fontWeight="500"
            >
              {n.label}
            </text>
          )}
        </g>
      ))}

      {/* Center "wayd" badge */}
      <g filter="url(#shadow)">
        <rect
          x="108"
          y="185"
          width="64"
          height="30"
          rx="8"
          fill="rgba(25,118,210,0.85)"
          stroke="rgba(100,181,246,0.4)"
          strokeWidth="1"
        />
        <text
          x="140"
          y="205"
          textAnchor="middle"
          fill="#fff"
          fontSize="13"
          fontFamily="'Segoe UI', system-ui, sans-serif"
          fontWeight="700"
          letterSpacing="2"
        >
          wayd
        </text>
      </g>
    </svg>
  )
}

function MicrosoftLogo() {
  return (
    <svg width="22" height="22" viewBox="0 0 21 21" fill="none">
      <rect x="1" y="1" width="9" height="9" fill="#f25022" rx="1" />
      <rect x="11" y="1" width="9" height="9" fill="#7fba00" rx="1" />
      <rect x="1" y="11" width="9" height="9" fill="#00a4ef" rx="1" />
      <rect x="11" y="11" width="9" height="9" fill="#ffb900" rx="1" />
    </svg>
  )
}

function LoadingSpinner() {
  return (
    <svg
      className={styles.spinner}
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
    >
      <circle
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="3"
        strokeLinecap="round"
        strokeDasharray="31.4 31.4"
      />
    </svg>
  )
}

function OidcLoginButton({
  provider,
  onLogin,
  isLoading,
}: {
  provider: OidcProviderInfo
  onLogin: (provider: OidcProviderInfo) => void
  isLoading: boolean
}) {
  const isMicrosoft =
    provider.providerType === OidcProviderType.MicrosoftEntraId

  return (
    <button
      type="button"
      onClick={() => onLogin(provider)}
      disabled={isLoading}
      className={styles.loginButton}
    >
      {isLoading ? (
        <>
          <LoadingSpinner />
          Signing in...
        </>
      ) : (
        <>
          {isMicrosoft && <MicrosoftLogo />}
          {provider.displayName}
        </>
      )}
    </button>
  )
}

function LocalLoginTab() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [rememberMe, setRememberMe] = useState(true)
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setIsSubmitting(true)

    try {
      const authClient = getAuthClient()
      const tokenResponse = await authClient.login({
        userName: username,
        password,
      })
      persistRememberMe(rememberMe)
      storeAuth(tokenResponse)
      // Full reload so AuthProvider hydrates from the freshly-stored token
      // and the layout transitions from unauthenticated view to app shell.
      window.location.href = '/'
    } catch (err: any) {
      const message =
        err?.detail ||
        err?.response?.data?.message ||
        err?.message ||
        'Invalid email or password.'
      setError(message)
      setIsSubmitting(false)
    }
  }

  return (
    <>
      <p className={styles.subtitle}>Sign in with your email and password.</p>

      <form onSubmit={handleSubmit} className={styles.localForm}>
        <input
          type="email"
          placeholder="Email"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          className={styles.input}
          autoComplete="email"
          required
          disabled={isSubmitting}
        />
        <input
          type="password"
          placeholder="Password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className={styles.input}
          autoComplete="current-password"
          required
          disabled={isSubmitting}
        />

        <label className={styles.rememberMeRow}>
          <input
            type="checkbox"
            checked={rememberMe}
            onChange={(e) => setRememberMe(e.target.checked)}
            className={styles.rememberMeCheckbox}
            disabled={isSubmitting}
          />
          <span className={styles.rememberMeLabel}>Remember me</span>
        </label>

        {error && <div className={styles.errorMessage}>{error}</div>}

        <button
          type="submit"
          disabled={isSubmitting || !username || !password}
          className={styles.loginButton}
        >
          {isSubmitting ? (
            <>
              <LoadingSpinner />
              Signing in...
            </>
          ) : (
            'Sign in'
          )}
        </button>
      </form>
    </>
  )
}

export default function LoginPage() {
  useDocumentTitle('Wayd Login')
  const router = useRouter()
  const { data: providers } = useGetAuthProvidersQuery()

  const oidcProviders = providers?.oidc ?? []
  const localEnabled = providers?.local !== false
  const hasOidcProviders = oidcProviders.length > 0

  // Tracks which OIDC provider button the user clicked so we can show a
  // spinner on the right button while the redirect is initiating.
  const [signingInProvider, setSigningInProvider] = useState<string | null>(null)

  // Exchange flow state. When we return from an OIDC redirect, the URL will
  // contain the `code` and `state` params. We detect this, complete the
  // callback, and exchange the access token for a Wayd JWT.
  const [exchangeState, setExchangeState] = useState<
    'idle' | 'running' | 'failed'
  >('idle')
  const [exchangeError, setExchangeError] = useState<string | null>(null)
  const exchangeStartedRef = useRef(false)
  const silentStartedRef = useRef(false)

  // Tab selection: only relevant when both OIDC and local are enabled.
  const [tabOverride, setTabOverride] = useState<'oidc' | 'local' | null>(null)
  const activeTab = tabOverride ?? (hasOidcProviders ? 'oidc' : 'local')
  const showTabs = hasOidcProviders && localEnabled

  // Already authenticated — bail back to the app.
  useEffect(() => {
    if (isAuthActive()) {
      router.replace('/')
    }
  }, [router])

  // Silent sign-in: if the user has visited before, try to re-authenticate
  // automatically using their IdP's existing SSO session. Runs once after
  // providers load, skipped when we're already handling a redirect callback.
  useEffect(() => {
    if (silentStartedRef.current) return
    if (isAuthActive()) return
    if (!providers) return

    // Skip if we're handling a redirect callback — that flow takes over.
    const url = new URL(window.location.href)
    if (url.searchParams.has('code') || url.searchParams.has('error')) return

    const lastProviderName = getLastProviderName()
    if (!lastProviderName) return

    const provider = providers.oidc.find((p) => p.name === lastProviderName)
    if (!provider) return

    silentStartedRef.current = true
    setExchangeState('running')

    const run = async () => {
      const oidcUser = await signinSilent(provider)
      if (!oidcUser) {
        // No active SSO session — show the login page normally.
        setExchangeState('idle')
        return
      }
      try {
        const tokenResponse = await getAuthClient().exchange({
          provider: provider.name,
          subjectToken: oidcUser.access_token,
        })
        storeAuth(tokenResponse)
        window.location.href = '/'
      } catch (error) {
        console.error('[Login] Silent exchange failed:', error)
        setExchangeState('idle')
      }
    }
    run()
  }, [providers])

  // OIDC redirect callback handler. Fires when we return from the IdP with a
  // `code` param in the URL. We stored the chosen provider name in
  // sessionStorage before the redirect; use it to match the right UserManager.
  useEffect(() => {
    if (exchangeStartedRef.current) return
    if (isAuthActive()) return

    const url = new URL(window.location.href)
    const hasCode = url.searchParams.has('code')
    const hasError = url.searchParams.has('error')
    if (!hasCode && !hasError) return

    const chosenProviderName = getChosenProviderName()
    if (!chosenProviderName) return

    // Providers may not be loaded yet — wait for them before completing signin.
    // This effect re-runs when `providers` changes, so it will retry.
    if (!providers) return

    const provider = providers.oidc.find((p) => p.name === chosenProviderName)
    if (!provider) return

    exchangeStartedRef.current = true
    setExchangeState('running')
    setExchangeError(null)

    const run = async () => {
      try {
        if (hasError) {
          const errorDesc = url.searchParams.get('error_description') ?? 'Sign-in was cancelled or failed.'
          throw new Error(errorDesc)
        }

        const oidcUser = await completeSignin(provider)
        clearChosenProvider()

        const tokenResponse = await getAuthClient().exchange({
          provider: provider.name,
          subjectToken: oidcUser.access_token,
        })
        storeAuth(tokenResponse)
        window.location.href = '/'
      } catch (error) {
        console.error('[Login] OIDC sign-in failed:', error)
        clearChosenProvider()
        setExchangeError(
          'Unable to complete sign-in. Please try again.',
        )
        setExchangeState('failed')
      }
    }
    run()
  }, [providers])

  const handleOidcLogin = async (provider: OidcProviderInfo) => {
    setSigningInProvider(provider.name)
    try {
      await signinRedirect(provider)
      // signinRedirect navigates away; code after this won't run.
    } catch (error) {
      console.error('[Login] OIDC redirect failed:', error)
      setSigningInProvider(null)
      setExchangeError('Unable to start sign-in. Please try again.')
    }
  }

  if (exchangeState === 'running') {
    return <LoadingAccount message="Completing sign-in…" />
  }

  return (
    <div className={styles.pageBackground}>
      <div className={`${styles.bgCircle} ${styles.bgCircle1}`} />
      <div className={`${styles.bgCircle} ${styles.bgCircle2}`} />
      <div className={`${styles.bgCircle} ${styles.bgCircle3}`} />

      <div className={styles.card}>
        {/* Left Panel */}
        <div className={styles.leftPanel}>
          <div className={styles.illustrationWrapper}>
            <AnalyticsIllustration />
          </div>

          <div className={styles.leftPanelText}>
            <h2 className={styles.leftPanelTitle}>Connect the dots.</h2>
            <p className={styles.leftPanelDescription}>
              Unify your teams, sprints, and planning intervals into one
              intelligent delivery platform.
            </p>
          </div>

          <div className={styles.decorativeDots}>
            {[0, 1, 2, 3, 4].map((i) => (
              <div
                key={i}
                className={`${styles.dot} ${i === 2 ? styles.dotActive : ''}`}
              />
            ))}
          </div>
        </div>

        {/* Right Panel */}
        <div className={styles.rightPanel}>
          <div className={styles.rightPanelContent}>
            {/* Logo */}
            <div className={styles.logo}>
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src="/wayd-icon.png"
                alt="wayd"
                className={styles.logoIcon}
              />
              <div className={styles.logoDivider} />
              <span className={styles.logoText}>wayd</span>
            </div>

            <h1 className={styles.title}>Welcome</h1>

            {exchangeError && (
              <div className={styles.errorMessage} role="alert">
                {exchangeError}
              </div>
            )}

            {/* Auth method tabs. Hidden when only one provider type is available. */}
            {showTabs && (
              <div className={styles.tabs}>
                <button
                  type="button"
                  className={`${styles.tab} ${activeTab === 'oidc' ? styles.tabActive : ''}`}
                  onClick={() => setTabOverride('oidc')}
                >
                  Sign in
                </button>
                <button
                  type="button"
                  className={`${styles.tab} ${activeTab === 'local' ? styles.tabActive : ''}`}
                  onClick={() => setTabOverride('local')}
                >
                  Email &amp; Password
                </button>
              </div>
            )}

            {activeTab === 'oidc' && hasOidcProviders ? (
              <>
                <p className={styles.subtitle}>
                  Sign in with your organizational account.
                </p>
                {oidcProviders.map((provider) => (
                  <OidcLoginButton
                    key={provider.name}
                    provider={provider}
                    onLogin={handleOidcLogin}
                    isLoading={signingInProvider === provider.name}
                  />
                ))}
              </>
            ) : (
              <LocalLoginTab />
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
