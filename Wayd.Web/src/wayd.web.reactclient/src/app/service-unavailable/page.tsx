'use client'

import { useEffect, useState } from 'react'
import dynamic from 'next/dynamic'
import { Button, Result, Space, Typography } from 'antd'
import { LogoutOutlined, ReloadOutlined } from '@ant-design/icons'
import styles from './page.module.css'

const DiagnosticTransition = dynamic(() => import('./diagnostic-transition'), { ssr: false })
const DiagnosticCanvas = dynamic(() => import('./diagnostic-canvas'), { ssr: false })

const { Paragraph } = Typography

interface ServiceUnavailablePageProps {
  onRetry?: () => void
  onLogout?: () => void
}

export default function ServiceUnavailablePage({
  onRetry,
  onLogout,
}: ServiceUnavailablePageProps) {
  const [apiReachable, setApiReachable] = useState<boolean | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    const checkApi = async () => {
      try {
        const apiUrl = process.env.NEXT_PUBLIC_API_BASE_URL
        if (!apiUrl) return

        // Use the unauthenticated /alive endpoint to check API availability.
        // Any HTTP response (even 404) proves the server is reachable.
        await fetch(`${apiUrl}/alive`, {
          method: 'GET',
          signal: controller.signal,
        })
        setApiReachable(true)
      } catch {
        // Network error or timeout - API is truly unreachable
        setApiReachable(false)
      }
    }

    checkApi()

    return () => controller.abort()
  }, [])

  const handleRetry = () => {
    if (onRetry) {
      onRetry()
    } else {
      window.location.reload()
    }
  }

  const handleLogout = () => {
    if (onLogout) {
      onLogout()
    } else {
      window.location.href = '/logout'
    }
  }

  const [dotClicks, setDotClicks] = useState(0)
  const [diagnosticStage, setDiagnosticStage] = useState<'idle' | 'transition' | 'diagnostic'>('idle')

  const isAuthIssue = apiReachable === true

  return (
    <div className={styles.container}>
      <div className={styles.content}>
        <Result
        status={isAuthIssue ? 'warning' : 500}
        title={isAuthIssue ? 'Session Error' : 'Wayd API Unavailable'}
        subTitle={
          isAuthIssue
            ? 'The server is running, but your session could not be verified. Signing out and back in typically resolves this.'
            : 'Wayd is unable to connect to the server. Please try again later.'
        }
        extra={
          <Space>
            <Button
              type="primary"
              key="retry"
              icon={<ReloadOutlined />}
              onClick={handleRetry}
            >
              Retry
            </Button>
            <Button
              key="logout"
              icon={<LogoutOutlined />}
              onClick={handleLogout}
            >
              Sign Out
            </Button>
          </Space>
        }
      >
        <div className="desc">
          {isAuthIssue ? (
            <>
              <Paragraph>
                This can happen when your authentication session expires or
                becomes invalid. Common causes include:
              </Paragraph>
              <ul>
                <li>
                  Your session expired while the app was open in multiple tabs
                </li>
                <li>Your account permissions were recently updated</li>
                <li>
                  A browser extension or network issue interrupted
                  authentication
                </li>
              </ul>
              <Paragraph>
                Click <strong>Sign Out</strong> and then sign back in. If this
                issue persists, please contact your administrator.
              </Paragraph>
            </>
          ) : (
            <>
              <Paragraph>
                The application is currently unable to reach the server. This
                could be due to:
              </Paragraph>
              <ul>
                <li>The server is temporarily unavailable</li>
                <li>Network connectivity issues</li>
                <li>The server is undergoing maintenance</li>
              </ul>
              <Paragraph>
                If this issue persists, please contact your administrator.
              </Paragraph>
            </>
          )}
        </div>
      </Result>
      <div
        className={styles.decorativeDots}
        onClick={() =>
          setDotClicks((prev) => {
            const next = Math.min(prev + 1, 5)
            if (next === 5) setDiagnosticStage('transition')
            return next
          })
        }
      >
        {[0, 1, 2, 3, 4].map((i) => (
          <div
            key={i}
            className={`${styles.dot} ${
              dotClicks === 0
                ? i === 2 ? styles.dotActive : ''
                : i < dotClicks ? styles.dotClicked : ''
            }`}
          />
        ))}
      </div>
      </div>
      {(diagnosticStage === 'transition' || diagnosticStage === 'diagnostic') && (
        <DiagnosticCanvas onClose={() => { setDiagnosticStage('idle'); setDotClicks(0) }} />
      )}
      {diagnosticStage === 'transition' && (
        <DiagnosticTransition onComplete={() => setDiagnosticStage('diagnostic')} />
      )}
    </div>
  )
}
