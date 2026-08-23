'use client'

import { PokerRoundDto } from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useResetPokerRoundMutation } from '@/src/store/features/planning/poker-sessions-api'
import { Button, Flex, Tag, Typography } from 'antd'
import { FC } from 'react'
import ResultsPanel from './results-panel'
import RoundLabelHeader from './round-label-header'
import styles from './poker-session.module.css'

const { Text } = Typography

export interface PokerReviewViewProps {
  round: PokerRoundDto
  sessionId: string
  sessionKey: number
  canManage: boolean
  participants: { id: string; name: string }[]
}

const PokerReviewView: FC<PokerReviewViewProps> = ({
  round,
  sessionId,
  sessionKey,
  canManage,
  participants,
}) => {
  const messageApi = useMessage()
  const [resetRound, { isLoading: isResetting }] =
    useResetPokerRoundMutation()

  const handleReset = async () => {
    try {
      const response = await resetRound({
        sessionId,
        roundId: round.id,
        sessionKey,
      })
      if (response.error) throw response.error
    } catch {
      messageApi.error('Failed to reset round.')
    }
  }

  // Build vote map for participant cards
  const voteMap = (() => {
    const map = new Map<string, string>()
    for (const vote of round.votes) {
      if (vote.participant) {
        map.set(vote.participant.id, vote.value)
      }
    }
    return map
  })()

  // Group votes by value for breakdown
  const voteGroups = (() => {
    const groups = new Map<string, string[]>()
    for (const vote of round.votes) {
      const names = groups.get(vote.value) ?? []
      names.push(vote.participant?.name ?? 'Unknown')
      groups.set(vote.value, names)
    }
    return [...groups.entries()].sort(([a], [b]) => {
      const na = parseFloat(a)
      const nb = parseFloat(b)
      if (!isNaN(na) && !isNaN(nb)) return na - nb
      return a.localeCompare(b)
    })
  })()

  return (
    <Flex vertical gap={16}>
      {/* Review header */}
      <div className={styles.reviewHeader}>
        <Flex align="center" gap={8} style={{ marginBottom: 8 }}>
          <Tag color="green">Estimated</Tag>
        </Flex>
        <RoundLabelHeader
          round={round}
          sessionId={sessionId}
          sessionKey={sessionKey}
          canManage={canManage}
        />
        {round.consensusEstimate && (
          <Text
            strong
            style={{
              fontSize: 28,
              color: 'var(--poker-success)',
            }}
          >
            {round.consensusEstimate} pts
          </Text>
        )}
      </div>

      {/* Revealed cards */}
      <Flex wrap gap={16} justify="center">
        {participants.map((p) => {
          const vote = voteMap.get(p.id)
          const hasVoted = vote !== undefined
          const cardClass = `${styles.participantCard}${!hasVoted ? ` ${styles.participantCardNoVote}` : ''}`

          return (
            <div key={p.id} style={{ textAlign: 'center' }}>
              <div className={cardClass}>
                {hasVoted ? (
                  <span className={styles.cardValue}>{vote}</span>
                ) : (
                  <span style={{ fontSize: 20, color: 'var(--poker-muted)' }}>
                    &mdash;
                  </span>
                )}
              </div>
              <div className={styles.cardName}>{p.name}</div>
            </div>
          )
        })}
      </Flex>

      {/* Accepted banner */}
      {round.consensusEstimate && (
        <Flex justify="center">
          <Tag
            color="green"
            style={{ fontSize: 14, padding: '4px 12px' }}
          >
            Accepted as {round.consensusEstimate} points
          </Tag>
        </Flex>
      )}

      {/* Stats panel */}
      <ResultsPanel votes={round.votes} />

      {/* Vote breakdown */}
      {voteGroups.length > 0 && (
        <Flex vertical gap={8}>
          <Text type="secondary" strong style={{ fontSize: 12, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
            Vote Breakdown
          </Text>
          {voteGroups.map(([value, names]) => (
            <div key={value} className={styles.voteBreakdownGroup}>
              <Flex align="center" gap={8}>
                <Tag
                  color={
                    value === round.consensusEstimate ? 'green' : 'default'
                  }
                  style={{ margin: 0 }}
                >
                  {value}
                </Tag>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {names.length}&times;
                </Text>
                <Flex wrap gap={4}>
                  {names.map((name, i) => (
                    <Tag key={i} style={{ margin: 0 }}>
                      {name}
                    </Tag>
                  ))}
                </Flex>
              </Flex>
            </div>
          ))}
        </Flex>
      )}

      {/* Reset button */}
      {canManage && (
        <Flex justify="center" style={{ marginTop: 8 }}>
          <Button onClick={handleReset} loading={isResetting}>
            Reset &amp; Re-vote
          </Button>
        </Flex>
      )}
    </Flex>
  )
}

export default PokerReviewView
