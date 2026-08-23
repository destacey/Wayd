'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useAddPokerRoundMutation } from '@/src/store/features/planning/poker-sessions-api'
import { PlusOutlined } from '@ant-design/icons'
import { Button, Flex, Input, Typography } from 'antd'
import { FC, useState } from 'react'
import styles from './poker-session.module.css'

const { Title, Text } = Typography

export interface PokerLobbyStateProps {
  isActive: boolean
  canManage: boolean
  sessionId: string
  sessionKey: number
}

const PokerLobbyState: FC<PokerLobbyStateProps> = ({
  isActive,
  canManage,
  sessionId,
  sessionKey,
}) => {
  const messageApi = useMessage()
  const [addRound, { isLoading }] = useAddPokerRoundMutation()
  const [label, setLabel] = useState('')

  const handleAdd = async () => {
    try {
      const response = await addRound({
        sessionId,
        sessionKey,
        request: { label: label.trim() || undefined },
      })
      if (response.error) throw response.error
      setLabel('')
    } catch {
      messageApi.error('Failed to add round.')
    }
  }

  if (!isActive) {
    return (
      <div className={styles.lobbyState}>
        <Title level={4} style={{ margin: '0 0 8px' }}>
          Session Completed
        </Title>
        <Text type="secondary">No rounds were created in this session.</Text>
      </div>
    )
  }

  return (
    <div className={styles.lobbyState}>
      <div className={styles.lobbyCards}>
        <div className={styles.lobbyCard} />
        <div className={styles.lobbyCard} />
        <div className={styles.lobbyCard} />
      </div>
      <Title level={4} style={{ margin: '0 0 8px' }}>
        Ready to estimate
      </Title>
      {canManage ? (
        <Flex gap={8} style={{ maxWidth: 360, width: '100%' }}>
          <Input
            placeholder="Work item ID or label (optional)"
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            onPressEnter={handleAdd}
            maxLength={512}
          />
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={handleAdd}
            loading={isLoading}
          >
            Start
          </Button>
        </Flex>
      ) : (
        <Text type="secondary">
          Waiting for the facilitator to start a round.
        </Text>
      )}
    </div>
  )
}

export default PokerLobbyState
