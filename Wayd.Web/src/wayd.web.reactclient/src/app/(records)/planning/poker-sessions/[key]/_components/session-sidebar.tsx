'use client'

import { PokerSessionDetailsDto } from '@/src/services/wayd-api'
import { Button, Divider } from 'antd'
import { FC } from 'react'
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

  const content = (
    <>
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
