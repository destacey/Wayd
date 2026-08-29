'use client'

import { useEffect } from 'react'
import styles from './page.module.css'

function LoadingSpinner() {
  return (
    <svg
      className={styles.spinner}
      width="48"
      height="48"
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

export default function LogoutPage() {
  // AuthGate renders this page when the path is /logout and no Wayd JWT is
  // in storage — meaning logout has already completed (clearAuth ran in
  // auth-context's logout action, and any OIDC signoutRedirect navigated here
  // as the post-logout destination). Just redirect to /login.
  useEffect(() => {
    window.location.replace('/login')
  }, [])

  return (
    <div className={styles.pageBackground}>
      <div className={`${styles.bgCircle} ${styles.bgCircle1}`} />
      <div className={`${styles.bgCircle} ${styles.bgCircle2}`} />
      <div className={`${styles.bgCircle} ${styles.bgCircle3}`} />

      <div className={styles.card}>
        <div className={styles.content}>
          <div className={styles.logo}>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src="/wayd-icon.png"
              alt="Wayd"
              className={styles.logoIcon}
            />
            <div className={styles.logoDivider} />
            <span className={styles.logoText}>wayd</span>
          </div>

          <div className={styles.spinnerWrapper}>
            <LoadingSpinner />
          </div>

          <h1 className={styles.title}>Signing out...</h1>
          <p className={styles.subtitle}>
            Please wait while we sign you out of your account.
          </p>
        </div>
      </div>
    </div>
  )
}
