'use client'

import { PresenceParticipant } from '@/src/hooks/use-poker-session-connection'
import { getAvatarColor } from '@/src/utils'
import { Avatar, Button, Divider, Flex, Typography } from 'antd'
import { FC } from 'react'
import styles from './poker-session.module.css'

const { Text } = Typography

export interface LobbyParticipantsProps {
  participants: PresenceParticipant[]
  canManage: boolean
  isActive: boolean
  onComplete: () => void
  isCompleting: boolean
}

const LobbyParticipants: FC<LobbyParticipantsProps> = ({
  participants,
  canManage,
  isActive,
  onComplete,
  isCompleting,
}) => {
  return (
    <div className={styles.sidebar}>
      <div className={styles.sidebarInner}>
        <div className={styles.sectionLabel}>
          Participants ({participants.length})
        </div>
        <div className={styles.lobbyParticipantList}>
          {participants.length === 0 ? (
            <Text
              type="secondary"
              style={{ padding: '16px', display: 'block' }}
            >
              Waiting for participants to join...
            </Text>
          ) : (
            participants.map((p) => (
              <Flex
                key={p.id}
                align="center"
                gap={10}
                className={styles.lobbyParticipantItem}
              >
                <Avatar
                  size="small"
                  style={{ backgroundColor: getAvatarColor(p.id) }}
                >
                  {p.name.charAt(0).toUpperCase()}
                </Avatar>
                <Text ellipsis style={{ fontSize: 13 }}>
                  {p.name}
                </Text>
              </Flex>
            ))
          )}
        </div>
        {canManage && isActive && (
          <>
            <Divider style={{ margin: 0 }} />
            <div style={{ padding: '12px 16px' }}>
              <Button
                danger
                block
                onClick={onComplete}
                loading={isCompleting}
              >
                Complete Session
              </Button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}

export default LobbyParticipants
