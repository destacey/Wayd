'use client'

import { PokerVoteDto } from '@/src/services/wayd-api'
import { Flex, Statistic, Tag, Typography } from 'antd'
import { FC } from 'react'
import styles from './poker-session.module.css'

const { Text } = Typography

export interface VoteStats {
  average: string
  mode: string
  low: string
  high: string
  hasConsensus: boolean
}

export function calculateVoteStats(votes: PokerVoteDto[]): VoteStats {
  const numericVotes = votes
    .map((v) => parseFloat(v.value))
    .filter((v) => !isNaN(v))
    .sort((a, b) => a - b)

  const average =
    numericVotes.length > 0
      ? (numericVotes.reduce((s, v) => s + v, 0) / numericVotes.length).toFixed(
          1,
        )
      : '-'

  // Mode: most frequent value (using string values)
  const valueCounts = new Map<string, number>()
  for (const vote of votes) {
    valueCounts.set(vote.value, (valueCounts.get(vote.value) ?? 0) + 1)
  }
  let maxCount = 0
  let modeValue = '-'
  for (const [value, count] of valueCounts) {
    if (count > maxCount) {
      maxCount = count
      modeValue = value
    }
  }

  const low = numericVotes.length > 0 ? String(numericVotes[0]) : '-'
  const high =
    numericVotes.length > 0
      ? String(numericVotes[numericVotes.length - 1])
      : '-'

  // Consensus: numeric scales use spread <= 3, non-numeric scales check unanimity
  const hasConsensus =
    numericVotes.length >= 2
      ? numericVotes[numericVotes.length - 1] - numericVotes[0] <= 3
      : numericVotes.length === 1
        ? true
        : votes.length > 0 && maxCount === votes.length

  return { average, mode: modeValue, low, high, hasConsensus }
}

export interface ResultsPanelProps {
  votes: PokerVoteDto[]
}

const ResultsPanel: FC<ResultsPanelProps> = ({ votes }) => {
  const stats = calculateVoteStats(votes)

  const distribution = (() => {
    const counts = new Map<string, number>()
    for (const vote of votes) {
      counts.set(vote.value, (counts.get(vote.value) ?? 0) + 1)
    }
    return [...counts.entries()].sort(([a], [b]) => {
      const na = parseFloat(a)
      const nb = parseFloat(b)
      if (!isNaN(na) && !isNaN(nb)) return na - nb
      return a.localeCompare(b)
    })
  })()

  if (votes.length === 0) return null

  return (
    <div className={styles.resultsPanel}>
      <Flex align="center" gap={8} style={{ marginBottom: 12 }}>
        {stats.hasConsensus ? (
          <Tag color="green">Consensus</Tag>
        ) : (
          <Tag color="orange">Spread — discuss</Tag>
        )}
      </Flex>

      <div className={styles.statsGrid}>
        <Statistic title="Average" value={stats.average} styles={{ content: { fontSize: 18 } }} />
        <Statistic title="Mode" value={stats.mode} styles={{ content: { fontSize: 18 } }} />
        <Statistic title="Low" value={stats.low} styles={{ content: { fontSize: 18 } }} />
        <Statistic title="High" value={stats.high} styles={{ content: { fontSize: 18 } }} />
      </div>

      {distribution.length > 0 && (
        <Flex vertical gap={4} style={{ marginTop: 16 }}>
          {distribution.map(([value, count]) => (
            <Flex key={value} align="center" gap={8}>
              <Text
                strong
                style={{ width: 32, textAlign: 'right', fontSize: 13 }}
              >
                {value}
              </Text>
              <div
                className={styles.distributionBar}
                style={{
                  width: `${Math.max((count / votes.length) * 100, 4)}%`,
                }}
              />
              <Text type="secondary" style={{ fontSize: 12 }}>
                {count}
              </Text>
            </Flex>
          ))}
        </Flex>
      )}
    </div>
  )
}

export default ResultsPanel
