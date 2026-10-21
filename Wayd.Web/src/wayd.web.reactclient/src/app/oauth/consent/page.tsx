'use client'

import { Suspense, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { Alert, Button, Card, Flex, Result, Spin, Typography } from 'antd'
import dayjs from 'dayjs'
import styles from './page.module.css'
import { getOAuthAuthorizationRequestsClient } from '@/src/services/clients'
import { OAuthAuthorizationRequestResponse } from '@/src/services/wayd-api'
import { useDocumentTitle, useFeatureFlag } from '@/src/hooks'
import { toApiError } from '@/src/utils/problem-details'
import { isFramed, navigateWithFullReload } from '@/src/utils/window-utils'
import { useApproveArmed } from './use-approve-armed'

const { Title, Text, Paragraph } = Typography

type Decision = 'approve' | 'deny'

type PageState =
  | { kind: 'loading' }
  | { kind: 'ready'; request: OAuthAuthorizationRequestResponse }
  | { kind: 'notFound' }
  | { kind: 'forbidden' }
  | { kind: 'unreachable' }
  | { kind: 'failed' }
  | { kind: 'decided'; decision: Decision }

/** Which page state a failed API call leads to. No status means no response arrived at all. */
const stateForError = (error: unknown): PageState => {
  const { status } = toApiError(error)
  if (status === undefined) return { kind: 'unreachable' }
  if (status === 404) return { kind: 'notFound' }
  if (status === 403) return { kind: 'forbidden' }
  return { kind: 'failed' }
}

const GoToWayd = () => <Button href="/">Go to Wayd</Button>

/** Where the browser goes after the decision, phrased for the person deciding. */
const Destination = ({
  request,
}: {
  request: OAuthAuthorizationRequestResponse
}) => {
  if (request.redirectIsLoopback) {
    return (
      <Paragraph>
        Afterwards you will return to an app on this computer.
      </Paragraph>
    )
  }
  if (!request.redirectHost) {
    return <Paragraph>Afterwards your browser opens an app.</Paragraph>
  }
  return (
    <>
      <Paragraph>
        Afterwards you will be sent to{' '}
        <Text strong className={styles.identity}>
          {request.redirectHost}
        </Text>
        .
      </Paragraph>
      {request.redirectHostDiffers && (
        <Alert
          type="warning"
          showIcon
          title="This sends you to a different site"
          description={`The application is at ${request.clientHost}, but you will be sent to ${request.redirectHost}. Approve only if you expected that.`}
        />
      )}
    </>
  )
}

const ConsentRequest = ({
  request,
  deciding,
  error,
  onDecide,
}: {
  request: OAuthAuthorizationRequestResponse
  deciding: Decision | null
  error: string | null
  onDecide: (decision: Decision) => void
}) => {
  const approveArmed = useApproveArmed()

  return (
    <Flex vertical gap="small">
      <Text type="secondary">An application wants to connect to Wayd</Text>
      <Title level={3} className={styles.identity} style={{ margin: 0 }}>
        {request.clientHost}
      </Title>
      <Text className={styles.identity}>
        It calls itself “{request.clientName}”.
      </Text>

      <Destination request={request} />

      <div>
        <Text strong>If you approve, it can:</Text>
        <ul className={styles.grants}>
          <li>use Wayd as you, through Wayd&apos;s MCP endpoint</li>
          <li>
            do anything your account can do: it gets all of your permissions
          </li>
        </ul>
      </div>
      <Paragraph type="secondary">
        You can disconnect it at any time from Account → Sessions. This request
        expires at {dayjs(request.expiresAt).format('h:mm A')}.
      </Paragraph>

      {error && <Alert type="error" showIcon title={error} />}

      <Flex gap="small" justify="end">
        <Button
          onClick={() => onDecide('deny')}
          disabled={deciding !== null}
          loading={deciding === 'deny'}
        >
          Deny
        </Button>
        <Button
          type="primary"
          onClick={() => onDecide('approve')}
          disabled={deciding !== null || !approveArmed}
          loading={deciding === 'approve'}
        >
          Approve
        </Button>
      </Flex>
    </Flex>
  )
}

const ConsentContent = () => {
  const handle = useSearchParams().get('request')
  const mcpServer = useFeatureFlag('mcp-server')

  // Framing headers protect the document; this covers a consent page reached
  // inside a frame some other way, such as client-side routing.
  const [framed] = useState(isFramed)
  const [state, setState] = useState<PageState>(
    handle ? { kind: 'loading' } : { kind: 'notFound' },
  )
  const [deciding, setDeciding] = useState<Decision | null>(null)
  const [decisionError, setDecisionError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  // State updates are not synchronous, so a second click in the same frame
  // would still see the buttons enabled; the ref stops it sending a second call.
  const decisionSent = useRef(false)

  useEffect(() => {
    if (!handle || framed) return
    let cancelled = false
    getOAuthAuthorizationRequestsClient()
      .get(handle)
      .then((request) => {
        if (!cancelled) setState({ kind: 'ready', request })
      })
      .catch((error) => {
        if (!cancelled) setState(stateForError(error))
      })
    return () => {
      cancelled = true
    }
  }, [handle, framed, attempt])

  const decide = async (decision: Decision) => {
    if (!handle || decisionSent.current) return
    decisionSent.current = true
    setDeciding(decision)
    setDecisionError(null)

    try {
      const client = getOAuthAuthorizationRequestsClient()
      const { redirectUrl } =
        decision === 'approve'
          ? await client.approve(handle)
          : await client.deny(handle)
      setState({ kind: 'decided', decision })
      navigateWithFullReload(redirectUrl)
    } catch (error) {
      const next = stateForError(error)
      if (next.kind === 'unreachable' || next.kind === 'failed') {
        // The call may still have reached Wayd and been answered. Trying again
        // is safe: a request already answered reads as no longer valid.
        decisionSent.current = false
        setDecisionError(
          next.kind === 'unreachable'
            ? 'Wayd could not be reached. Please try again.'
            : 'Wayd could not complete that. Please try again.',
        )
      } else {
        setState(next)
      }
      setDeciding(null)
    }
  }

  const retry = (
    <Button
      type="primary"
      onClick={() => {
        setState({ kind: 'loading' })
        setAttempt((n) => n + 1)
      }}
    >
      Try again
    </Button>
  )

  if (framed) {
    return (
      <Result
        status="error"
        title="This page cannot be shown inside another site"
        subTitle="Open the connection request in its own browser tab."
      />
    )
  }

  switch (state.kind) {
    case 'loading':
      return (
        <Flex justify="center">
          <Spin size="large" />
        </Flex>
      )
    case 'ready':
      return (
        <ConsentRequest
          request={state.request}
          deciding={deciding}
          error={decisionError}
          onDecide={decide}
        />
      )
    case 'notFound':
      // The API answers 404 while the flag is off as well, so the flag tells the two apart.
      if (mcpServer.isLoading) {
        return (
          <Flex justify="center">
            <Spin size="large" />
          </Flex>
        )
      }
      return mcpServer.isEnabled ? (
        <Result
          status="warning"
          title="This connection request is no longer valid"
          subTitle="It has expired or was already answered. Go back to the application and connect again."
          extra={<GoToWayd />}
        />
      ) : (
        <Result
          status="info"
          title="Connecting applications is not turned on"
          subTitle="An administrator has not enabled the MCP server for this Wayd organization."
          extra={<GoToWayd />}
        />
      )
    case 'forbidden':
      return (
        <Result
          status="403"
          title="Sign in to Wayd to answer this request"
          subTitle="Only a session signed in to the Wayd web app can approve a connection."
          extra={<GoToWayd />}
        />
      )
    case 'unreachable':
      return (
        <Result
          status="error"
          title="Wayd could not be reached"
          subTitle="Check your connection and try again."
          extra={retry}
        />
      )
    case 'failed':
      return (
        <Result
          status="error"
          title="Something went wrong"
          subTitle="Wayd could not load this connection request. Try again in a moment."
          extra={retry}
        />
      )
    case 'decided':
      return (
        <Result
          status={state.decision === 'approve' ? 'success' : 'info'}
          title={
            state.decision === 'approve'
              ? 'Connection approved'
              : 'Connection denied'
          }
          subTitle="Returning you to the application. You can close this tab once it opens."
          extra={<GoToWayd />}
        />
      )
  }
}

/** Where a user approves or denies an application's request to connect to Wayd on their behalf. */
const ConsentPage = () => {
  useDocumentTitle('Connect an application')

  return (
    <main className={styles.page}>
      <Card className={styles.card}>
        <div className={styles.brand}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/wayd-icon.png" alt="" className={styles.brandIcon} />
          <Text type="secondary">Wayd</Text>
        </div>
        <Suspense
          fallback={
            <Flex justify="center">
              <Spin size="large" />
            </Flex>
          }
        >
          <ConsentContent />
        </Suspense>
      </Card>
    </main>
  )
}

export default ConsentPage
