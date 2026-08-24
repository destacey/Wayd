'use client'

import { useEffect, useRef, useState } from 'react'
import { getAuthClient, setRememberMe, storeAuth } from '@/src/services/clients'
import { useDocumentTitle } from '@/src/hooks'
import { notFound } from 'next/navigation'

function LoadingSpinner() {
  return (
    <svg
      style={{ animation: 'spin 1s linear infinite' }}
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
    >
      <style>{`@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }`}</style>
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

export default function SetupPage() {
  useDocumentTitle('Wayd Setup')

  const [status, setStatus] = useState<'checking' | 'ready' | 'done' | 'unavailable'>('checking')
  const [token, setToken] = useState('')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [passwordFieldError, setPasswordFieldError] = useState('')
  const [confirmFieldError, setConfirmFieldError] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const checkStarted = useRef(false)

  useEffect(() => {
    if (checkStarted.current) return
    checkStarted.current = true

    // Check if bootstrap is available by attempting a GET-like probe.
    // The setup endpoint returns 409 if already complete, 400 for bad token.
    // We POST with an empty token — a 400 means the endpoint is live (token
    // is active, just rejected), a 409 means already complete.
    const probe = async () => {
      try {
        await getAuthClient().setup({ token: '', firstName: '', lastName: '', email: '', password: '' })
        // Should not reach here on empty token, but if it does setup worked.
        setStatus('done')
      } catch (err: any) {
        const status = err?.response?.status ?? err?.status
        if (status === 400) {
          // 400 = bad token, but the endpoint is active — bootstrap is available.
          setStatus('ready')
        } else if (status === 409) {
          // Already complete.
          setStatus('unavailable')
        } else {
          // Any other error (network, 500) — treat as unavailable.
          setStatus('unavailable')
        }
      }
    }
    probe()
  }, [])

  const validatePassword = (pw: string): string => {
    if (pw.length < 8) return 'Password must be at least 8 characters.'
    if (!/[A-Z]/.test(pw)) return 'Password must contain at least one uppercase letter.'
    if (!/[a-z]/.test(pw)) return 'Password must contain at least one lowercase letter.'
    if (!/[0-9]/.test(pw)) return 'Password must contain at least one digit.'
    return ''
  }

  const handlePasswordBlur = () => {
    setPasswordFieldError(validatePassword(password))
  }

  const handleConfirmBlur = () => {
    if (confirmPassword && password !== confirmPassword) {
      setConfirmFieldError('Passwords do not match.')
    } else {
      setConfirmFieldError('')
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    const pwErr = validatePassword(password)
    if (pwErr) {
      setPasswordFieldError(pwErr)
      return
    }
    if (password !== confirmPassword) {
      setConfirmFieldError('Passwords do not match.')
      return
    }

    setIsSubmitting(true)

    try {
      const tokenResponse = await getAuthClient().setup({
        token,
        firstName,
        lastName,
        email,
        password,
      })
      setRememberMe(true)
      storeAuth(tokenResponse)
      window.location.href = '/'
    } catch (err: any) {
      const message =
        err?.response?.data?.detail ||
        err?.detail ||
        err?.message ||
        'Setup failed. Check your token and try again.'
      setError(message)
      setIsSubmitting(false)
    }
  }

  if (status === 'unavailable') {
    notFound()
  }

  const isLoading = status === 'checking'

  return (
    <div style={styles.pageBackground}>
      <div style={{...styles.bgCircle, top: -120, left: -100, width: 340, height: 340, background: 'rgba(255,255,255,0.06)'}} />
      <div style={{...styles.bgCircle, bottom: -80, right: -60, width: 260, height: 260, background: 'rgba(255,255,255,0.05)'}} />

      <div style={styles.card}>
        {/* Left panel */}
        <div style={styles.leftPanel}>
          <div style={styles.leftPanelText}>
            <h2 style={styles.leftPanelTitle}>First-time setup</h2>
            <p style={styles.leftPanelDescription}>
              Create the first administrator account to get started. You&apos;ll
              use this account to configure identity providers and invite your
              team.
            </p>
          </div>
          <ol style={styles.steps}>
            <li style={styles.step}><span style={styles.stepNum}>1</span> Create admin account</li>
            <li style={styles.step}><span style={styles.stepNum}>2</span> Configure identity provider</li>
            <li style={styles.step}><span style={styles.stepNum}>3</span> Invite your team</li>
          </ol>
        </div>

        {/* Right panel */}
        <div style={styles.rightPanel}>
          <div style={styles.rightPanelContent}>
            <div style={styles.logo}>
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src="/wayd-icon.png" alt="wayd" style={styles.logoIcon} />
              <div style={styles.logoDivider} />
              <span style={styles.logoText}>wayd</span>
            </div>

            <h1 style={styles.title}>Welcome</h1>
            <p style={styles.subtitle}>
              Enter the setup token from your server logs to create the first admin account.
            </p>

            {isLoading ? (
              <div style={styles.loadingRow}>
                <LoadingSpinner />
                <span style={styles.loadingText}>Checking setup status…</span>
              </div>
            ) : (
              <form onSubmit={handleSubmit} style={styles.form} noValidate>
                <input
                  type="text"
                  placeholder="Setup token (from server logs)"
                  value={token}
                  onChange={(e) => setToken(e.target.value)}
                  style={styles.input}
                  required
                  disabled={isSubmitting}
                  autoComplete="off"
                />

                <div style={styles.nameRow}>
                  <input
                    type="text"
                    placeholder="First name"
                    value={firstName}
                    onChange={(e) => setFirstName(e.target.value)}
                    style={styles.input}
                    required
                    disabled={isSubmitting}
                    autoComplete="given-name"
                  />
                  <input
                    type="text"
                    placeholder="Last name"
                    value={lastName}
                    onChange={(e) => setLastName(e.target.value)}
                    style={styles.input}
                    required
                    disabled={isSubmitting}
                    autoComplete="family-name"
                  />
                </div>

                <input
                  type="email"
                  placeholder="Email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  style={styles.input}
                  required
                  disabled={isSubmitting}
                  autoComplete="email"
                />
                <input
                  type="password"
                  placeholder="Password"
                  value={password}
                  onChange={(e) => { setPassword(e.target.value); setPasswordFieldError('') }}
                  onBlur={handlePasswordBlur}
                  style={{...styles.input, ...(passwordFieldError ? styles.inputError : {})}}
                  disabled={isSubmitting}
                  autoComplete="new-password"
                />
                {passwordFieldError && <div style={styles.fieldError} role="alert">{passwordFieldError}</div>}
                <input
                  type="password"
                  placeholder="Confirm password"
                  value={confirmPassword}
                  onChange={(e) => { setConfirmPassword(e.target.value); setConfirmFieldError('') }}
                  onBlur={handleConfirmBlur}
                  style={{...styles.input, ...(confirmFieldError ? styles.inputError : {})}}
                  disabled={isSubmitting}
                  autoComplete="new-password"
                />
                {confirmFieldError && <div style={styles.fieldError} role="alert">{confirmFieldError}</div>}

                {error && <div style={styles.errorMessage} role="alert">{error}</div>}

                <button
                  type="submit"
                  disabled={isSubmitting || !token || !firstName || !lastName || !email || !password || !confirmPassword}
                  style={{
                    ...styles.submitButton,
                    ...(isSubmitting ? styles.submitButtonDisabled : {}),
                  }}
                >
                  {isSubmitting ? (
                    <span style={styles.buttonInner}>
                      <LoadingSpinner />
                      Creating account…
                    </span>
                  ) : (
                    'Create admin account'
                  )}
                </button>
              </form>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

const styles: Record<string, React.CSSProperties> = {
  pageBackground: {
    position: 'fixed',
    top: 0, left: 0, right: 0, bottom: 0,
    width: '100%',
    height: '100dvh',
    overflowX: 'hidden',
    overflowY: 'auto',
    fontFamily: "'Segoe UI', system-ui, -apple-system, sans-serif",
    background: 'linear-gradient(135deg, #42a5f5 0%, #2196f3 40%, #1976d2 100%)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '40px 20px',
  },
  bgCircle: {
    position: 'absolute',
    borderRadius: '50%',
    pointerEvents: 'none',
  },
  card: {
    display: 'flex',
    width: 'min(66vw, 1100px)',
    minWidth: 680,
    minHeight: 'min(82vh, 600px)',
    borderRadius: 8,
    overflow: 'hidden',
    boxShadow: '0 25px 60px rgba(13, 71, 161, 0.35)',
  },
  leftPanel: {
    flex: '1 1 45%',
    minWidth: 0,
    background: 'linear-gradient(160deg, #0d47a1 0%, #1565c0 100%)',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '48px 32px',
  },
  leftPanelText: {
    textAlign: 'center',
    marginBottom: 40,
  },
  leftPanelTitle: {
    color: '#fff',
    fontSize: 22,
    fontWeight: 700,
    letterSpacing: -0.3,
    marginBottom: 12,
  },
  leftPanelDescription: {
    color: 'rgba(187, 222, 251, 0.75)',
    fontSize: 13,
    maxWidth: 260,
    margin: '0 auto',
    lineHeight: 1.6,
  },
  steps: {
    listStyle: 'none',
    padding: 0,
    margin: 0,
    display: 'flex',
    flexDirection: 'column',
    gap: 16,
    width: '100%',
    maxWidth: 220,
  },
  step: {
    display: 'flex',
    alignItems: 'center',
    gap: 12,
    color: 'rgba(187, 222, 251, 0.85)',
    fontSize: 14,
    fontWeight: 500,
  },
  stepNum: {
    width: 28,
    height: 28,
    borderRadius: '50%',
    background: 'rgba(255,255,255,0.15)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: 12,
    fontWeight: 700,
    color: '#fff',
    flexShrink: 0,
  },
  rightPanel: {
    flex: '1 1 55%',
    minWidth: 0,
    background: '#fff',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '40px 48px',
  },
  rightPanelContent: {
    width: '100%',
    maxWidth: 380,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
  },
  logo: {
    display: 'flex',
    alignItems: 'center',
    gap: 16,
    marginBottom: 32,
  },
  logoIcon: {
    width: 48,
    height: 48,
    objectFit: 'contain',
  },
  logoDivider: {
    width: 1,
    height: 40,
    background: '#90caf9',
  },
  logoText: {
    fontSize: 32,
    fontWeight: 600,
    color: '#1976d2',
    letterSpacing: -0.5,
  },
  title: {
    fontSize: 26,
    fontWeight: 700,
    color: '#0d47a1',
    letterSpacing: -0.5,
    marginBottom: 8,
    textAlign: 'center',
  },
  subtitle: {
    fontSize: 13.5,
    color: '#6b7280',
    textAlign: 'center',
    lineHeight: 1.5,
    maxWidth: 300,
    marginBottom: 32,
  },
  loadingRow: {
    display: 'flex',
    alignItems: 'center',
    gap: 12,
    color: '#6b7280',
  },
  loadingText: {
    fontSize: 14,
  },
  form: {
    width: '100%',
    display: 'flex',
    flexDirection: 'column',
    gap: 12,
  },
  nameRow: {
    display: 'flex',
    gap: 12,
  },
  input: {
    width: '100%',
    padding: '12px 16px',
    border: '1.5px solid #bbdefb',
    borderRadius: 4,
    fontFamily: "'Segoe UI', system-ui, -apple-system, sans-serif",
    fontSize: 14,
    color: '#0d47a1',
    background: '#fff',
    outline: 'none',
    boxSizing: 'border-box',
    transition: 'border-color 0.2s, box-shadow 0.2s',
  },
  submitButton: {
    width: '100%',
    padding: '14px 24px',
    border: 'none',
    borderRadius: 4,
    background: '#1976d2',
    color: '#fff',
    fontFamily: "'Segoe UI', system-ui, -apple-system, sans-serif",
    fontSize: 15,
    fontWeight: 600,
    cursor: 'pointer',
    transition: 'background 0.2s, transform 0.15s',
    marginTop: 4,
  },
  submitButtonDisabled: {
    opacity: 0.7,
    cursor: 'not-allowed',
  },
  buttonInner: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 10,
  },
  inputError: {
    borderColor: '#d32f2f',
  },
  fieldError: {
    color: '#d32f2f',
    fontSize: 12,
    marginTop: -4,
  },
  errorMessage: {
    color: '#d32f2f',
    fontSize: 13,
    padding: '8px 12px',
    background: '#fce4ec',
    borderRadius: 4,
    textAlign: 'center',
  },
}
