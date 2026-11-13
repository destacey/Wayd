'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  PokerRoundPublicationOutcome,
  PokerSessionDetailsDto,
} from '@/src/services/wayd-api'
import { usePublishPokerRoundsMutation } from '@/src/store/features/planning/poker-sessions-api'
import { toApiError } from '@/src/utils'
import { Button, Divider, Flex } from 'antd'
import { FC } from 'react'
import AddDeskRequestsButton, { hostDeskId } from './add-desk-requests-button'
import { canPublishRound } from './round-publication'
import SessionSummary from './session-summary'
import SessionTimeline from './session-timeline'
import styles from './poker-session.module.css'

export interface SessionSidebarProps {
  session: PokerSessionDetailsDto
  selectedRoundId?: string
  onSelectRound: (roundId: string) => void
  onRemoveRound: (roundId: string) => void
  onComplete: () => void
  isCompleting: boolean
  canManage: boolean
  isActive: boolean
  /** When true, renders content without the outer sidebar wrapper (for use inside a Drawer). */
  inline?: boolean
}

const SessionSidebar: FC<SessionSidebarProps> = ({
  session,
  selectedRoundId,
  onSelectRound,
  onRemoveRound,
  onComplete,
  isCompleting,
  canManage,
  isActive,
  inline,
}) => {
  const rounds = session.rounds ?? []
  const messageApi = useMessage()
  const [publish, { isLoading: isPublishing }] = usePublishPokerRoundsMutation()

  const unpublished = rounds.filter(canPublishRound).length
  const deskId = hostDeskId(session)

  const handlePublishAll = async () => {
    try {
      const published = await publish({
        sessionId: session.id,
        sessionKey: session.key,
      }).unwrap()
      const refused = published.filter(
        (r) => r.publication?.outcome === PokerRoundPublicationOutcome.Refused,
      ).length
      if (refused > 0) {
        messageApi.warning(
          `${published.length - refused} published, ${refused} refused. Open a refused round to see why.`,
        )
      } else {
        messageApi.success(
          `${published.length} ${published.length === 1 ? 'estimate' : 'estimates'} published.`,
        )
      }
    } catch (error) {
      messageApi.error(
        toApiError(error).detail ?? 'Failed to publish the estimates.',
      )
    }
  }

  const content = (
    <>
      {canManage && (unpublished > 0 || (deskId && isActive)) && (
        <Flex vertical gap={8} style={{ padding: '12px 16px' }}>
          {unpublished > 0 && (
            <Button
              type="primary"
              block
              onClick={handlePublishAll}
              loading={isPublishing}
            >
              Publish {unpublished}{' '}
              {unpublished === 1 ? 'estimate' : 'estimates'}
            </Button>
          )}
          {isActive && <AddDeskRequestsButton session={session} block />}
        </Flex>
      )}
      {canManage && isActive && (
        <>
          <Divider style={{ margin: 0 }} />
          <div style={{ padding: '12px 16px' }}>
            <Button danger block onClick={onComplete} loading={isCompleting}>
              Complete Session
            </Button>
          </div>
        </>
      )}
      <SessionSummary rounds={rounds} />
      {isActive && <Divider style={{ margin: 0 }} />}
      <SessionTimeline
        rounds={rounds}
        selectedRoundId={selectedRoundId}
        onSelectRound={onSelectRound}
        onRemoveRound={onRemoveRound}
        canManage={canManage}
        sessionId={session.id}
        sessionKey={session.key}
        isActive={isActive}
      />
    </>
  )

  if (inline) return content

  return (
    <div className={styles.sidebar}>
      <div className={styles.sidebarInner}>{content}</div>
    </div>
  )
}

export default SessionSidebar
