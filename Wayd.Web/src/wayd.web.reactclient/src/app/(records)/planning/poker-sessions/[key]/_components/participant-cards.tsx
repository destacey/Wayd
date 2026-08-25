'use client'

import { PokerVoteDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'
import { CSSProperties, FC } from 'react'
import styles from './poker-session.module.css'

export interface ParticipantCardsProps {
  participants: { id: string; name: string }[]
  votes: PokerVoteDto[]
  isRevealed: boolean
}

const ParticipantCards: FC<ParticipantCardsProps> = ({
  participants,
  votes,
  isRevealed,
}) => {
  const voteMap = (() => {
    const map = new Map<string, string>()
    for (const vote of votes) {
      if (vote.participant) {
        map.set(vote.participant.id, vote.value)
      }
    }
    return map
  })()

  return (
    <Flex wrap gap={16} justify="center">
      {participants.map((p, index) => {
        const vote = voteMap.get(p.id)
        const hasVoted = vote !== undefined

        const frontClass = `${styles.cardFront} ${
          hasVoted ? styles.cardFrontVoted : styles.cardFrontPending
        }`

        const backClass = `${styles.cardBack} ${
          !hasVoted ? styles.cardBackNoVote : ''
        }`

        const containerClass = `${styles.cardFlipContainer} ${
          isRevealed ? styles.cardFlipRevealed : ''
        }`

        return (
          <div key={p.id} style={{ textAlign: 'center' }}>
            <div
              className={containerClass}
              style={
                {
                  '--stagger-delay': `${index * 100}ms`,
                } as CSSProperties
              }
            >
              <div className={styles.cardFlipInner}>
                {/* Front face: shown during voting */}
                <div className={frontClass}>
                  {hasVoted && (
                    <span className={styles.cardSymbol}>&#9824;</span>
                  )}
                </div>
                {/* Back face: shown after reveal */}
                <div className={backClass}>
                  {hasVoted ? (
                    <span className={styles.cardValue}>{vote}</span>
                  ) : (
                    <span
                      style={{ fontSize: 20, color: 'var(--poker-muted)' }}
                    >
                      &mdash;
                    </span>
                  )}
                </div>
              </div>
            </div>
            <div className={styles.cardName}>{p.name}</div>
          </div>
        )
      })}
    </Flex>
  )
}

export default ParticipantCards
