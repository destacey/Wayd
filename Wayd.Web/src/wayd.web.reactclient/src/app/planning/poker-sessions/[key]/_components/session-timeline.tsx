'use client'

import {
  PokerRoundDto,
  PokerRoundPublicationOutcome,
} from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useAddPokerRoundMutation } from '@/src/store/features/planning/poker-sessions-api'
import {
  CheckCircleOutlined,
  DeleteOutlined,
  ExclamationCircleOutlined,
  PlusOutlined,
} from '@ant-design/icons'
import {
  Button,
  Flex,
  Input,
  Popconfirm,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import { FC, useState } from 'react'
import styles from './poker-session.module.css'
import { roundTitle } from './round-title'

const { Text } = Typography

export interface SessionTimelineProps {
  rounds: PokerRoundDto[]
  selectedRoundId?: string
  onSelectRound: (roundId: string) => void
  onRemoveRound: (roundId: string) => void
  canManage: boolean
  sessionId: string
  sessionKey: number
  isActive: boolean
}

const SessionTimeline: FC<SessionTimelineProps> = ({
  rounds,
  selectedRoundId,
  onSelectRound,
  onRemoveRound,
  canManage,
  sessionId,
  sessionKey,
  isActive,
}) => {
  const messageApi = useMessage()
  const [addRound, { isLoading: isAdding }] = useAddPokerRoundMutation()
  const [newRoundLabel, setNewRoundLabel] = useState('')

  const handleAddRound = async () => {
    const label = newRoundLabel.trim()
    if (!label) return
    try {
      const response = await addRound({
        sessionId,
        sessionKey,
        request: { label },
      })
      if (response.error) throw response.error
      setNewRoundLabel('')
    } catch {
      messageApi.error('Failed to add round.')
    }
  }

  const { activeRounds, completedRounds } = (() => {
    const sorted = [...rounds].sort((a, b) => a.order - b.order)
    return {
      activeRounds: sorted.filter(
        (r) => r.status === 'Voting' || r.status === 'Revealed',
      ),
      completedRounds: sorted.filter((r) => r.status === 'Accepted'),
    }
  })()

  // Approximate from the round with the most votes
  const totalParticipants = rounds.reduce((max, r) => Math.max(max, r.voteCount), 0)

  return (
    <div>
      {(activeRounds.length > 0 || isActive) && (
        <div className={styles.sectionLabel}>Timeline</div>
      )}
      <div className={styles.timelineList}>
        {activeRounds.map((round) => {
          const isSelected = round.id === selectedRoundId
          const itemClass = `${styles.timelineItem}${isSelected ? ` ${styles.timelineItemActive}` : ''}`

          return (
            <Flex
              key={round.id}
              className={itemClass}
              align="center"
              justify="space-between"
              onClick={() => onSelectRound(round.id)}
            >
              <Flex align="center" gap={8} style={{ minWidth: 0, flex: 1 }}>
                <span className={styles.pulsingDot} />
                <Flex vertical style={{ minWidth: 0, flex: 1 }}>
                  <Text
                    ellipsis={{ tooltip: roundTitle(round) }}
                    style={{ fontSize: 13 }}
                  >
                    {roundTitle(round) ?? 'Untitled round'}
                  </Text>
                  <Text type="secondary" style={{ fontSize: 11 }}>
                    In Progress
                  </Text>
                </Flex>
              </Flex>
              <Flex align="center" gap={4}>
                {(round.voteCount > 0 || round.status === 'Revealed') && (
                  <Tag color="blue" style={{ margin: 0, fontSize: 11 }}>
                    {round.voteCount}/{totalParticipants || '?'}
                  </Tag>
                )}
                {canManage && isActive && (
                  <Popconfirm
                    title="Remove this round?"
                    onConfirm={(e) => {
                      e?.stopPropagation()
                      onRemoveRound(round.id)
                    }}
                    onCancel={(e) => e?.stopPropagation()}
                  >
                    <Button
                      type="text"
                      size="small"
                      danger
                      icon={<DeleteOutlined />}
                      onClick={(e) => e.stopPropagation()}
                    />
                  </Popconfirm>
                )}
              </Flex>
            </Flex>
          )
        })}

        {canManage && isActive && (
          <Flex gap={8} style={{ padding: '8px 16px' }}>
            <Input
              size="small"
              placeholder="Add a round: what are you estimating?"
              value={newRoundLabel}
              onChange={(e) => setNewRoundLabel(e.target.value)}
              onPressEnter={handleAddRound}
              maxLength={512}
            />
            <Button
              size="small"
              type="text"
              icon={<PlusOutlined />}
              onClick={handleAddRound}
              loading={isAdding}
              disabled={!newRoundLabel.trim()}
            />
          </Flex>
        )}

        {completedRounds.length > 0 && (
          <>
            <div className={styles.timelineDivider} />
            <div className={styles.timelineDividerLabel}>Completed</div>
          </>
        )}

        {completedRounds.map((round) => {
          const isSelected = round.id === selectedRoundId
          const itemClass = `${styles.timelineItem}${isSelected ? ` ${styles.timelineItemActive}` : ''}`

          return (
            <Flex
              key={round.id}
              className={itemClass}
              align="center"
              justify="space-between"
              onClick={() => onSelectRound(round.id)}
            >
              <Text
                ellipsis={{ tooltip: roundTitle(round) }}
                style={{ fontSize: 13, minWidth: 0, flex: 1 }}
              >
                {roundTitle(round) ?? 'Untitled round'}
              </Text>
              <Flex align="center" gap={4}>
                {round.publication?.outcome ===
                  PokerRoundPublicationOutcome.Published && (
                  <Tooltip title="Published">
                    <CheckCircleOutlined
                      aria-label="Published"
                      style={{ color: 'var(--poker-success)' }}
                    />
                  </Tooltip>
                )}
                {round.publication?.outcome ===
                  PokerRoundPublicationOutcome.Refused && (
                  <Tooltip title={round.publication.refusalReason}>
                    <ExclamationCircleOutlined
                      aria-label="Not published"
                      style={{ color: 'var(--poker-warning)' }}
                    />
                  </Tooltip>
                )}
                {round.consensusEstimate && (
                  <Tag color="green" style={{ margin: 0, fontSize: 11 }}>
                    {round.consensusEstimate}
                  </Tag>
                )}
                {canManage && isActive && (
                  <Popconfirm
                    title="Remove this round?"
                    onConfirm={(e) => {
                      e?.stopPropagation()
                      onRemoveRound(round.id)
                    }}
                    onCancel={(e) => e?.stopPropagation()}
                  >
                    <Button
                      type="text"
                      size="small"
                      danger
                      icon={<DeleteOutlined />}
                      onClick={(e) => e.stopPropagation()}
                    />
                  </Popconfirm>
                )}
              </Flex>
            </Flex>
          )
        })}
      </div>
    </div>
  )
}

export default SessionTimeline
