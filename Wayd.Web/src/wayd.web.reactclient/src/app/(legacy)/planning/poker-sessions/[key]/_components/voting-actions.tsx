'use client'

import { PokerRoundDto } from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useRevealPokerRoundMutation,
  useResetPokerRoundMutation,
  useSetPokerRoundConsensusMutation,
} from '@/src/store/features/planning/poker-sessions-api'
import { Button, Dropdown, Space } from 'antd'
import type { MenuProps } from 'antd'
import { DownOutlined } from '@ant-design/icons'
import { FC } from 'react'
import { calculateVoteStats } from './results-panel'
import styles from './poker-session.module.css'

const { Compact: SpaceCompact } = Space
export interface VotingActionsProps {
  round: PokerRoundDto
  sessionId: string
  sessionKey: number
  estimationScaleValues: string[]
  canManage: boolean
  onConsensusSet?: () => void
}

const VotingActions: FC<VotingActionsProps> = ({
  round,
  sessionId,
  sessionKey,
  estimationScaleValues,
  canManage,
  onConsensusSet,
}) => {
  const messageApi = useMessage()

  const [revealRound, { isLoading: isRevealing }] =
    useRevealPokerRoundMutation()
  const [resetRound, { isLoading: isResetting }] = useResetPokerRoundMutation()
  const [setConsensus, { isLoading: isSettingConsensus }] =
    useSetPokerRoundConsensusMutation()

  const handleReveal = async () => {
    try {
      const response = await revealRound({
        sessionId,
        roundId: round.id,
        sessionKey,
      })
      if (response.error) throw response.error
    } catch {
      messageApi.error('Failed to reveal votes.')
    }
  }

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

  const handleSetConsensus = async (value: string) => {
    try {
      const response = await setConsensus({
        sessionId,
        roundId: round.id,
        sessionKey,
        request: { estimate: value },
      })
      if (response.error) throw response.error
      messageApi.success('Consensus set.')
      onConsensusSet?.()
    } catch {
      messageApi.error('Failed to set consensus.')
    }
  }

  const modeValue =
    round.status !== 'Revealed' || round.votes.length === 0
      ? undefined
      : calculateVoteStats(round.votes).mode

  const isVoting = round.status === 'Voting'
  const isRevealed = round.status === 'Revealed'

  if (!canManage) return null

  const overrideMenuItems: MenuProps['items'] = estimationScaleValues.map(
    (v) => ({
      key: v,
      label: v,
    }),
  )

  const handleAcceptDefault = () => {
    if (modeValue && modeValue !== '-') handleSetConsensus(modeValue)
  }

  const handleAcceptOverride: MenuProps['onClick'] = ({ key }) => {
    handleSetConsensus(key)
  }

  return (
    <div className={styles.votingActions}>
      {isVoting && (
        <Button
          type="primary"
          size="large"
          onClick={handleReveal}
          loading={isRevealing}
        >
          Reveal Cards
        </Button>
      )}
      {isRevealed && (
        <>
          <Button onClick={handleReset} loading={isResetting}>
            Re-vote
          </Button>
          <SpaceCompact>
            <Button
              type="primary"
              onClick={handleAcceptDefault}
              disabled={!modeValue || modeValue === '-'}
              loading={isSettingConsensus}
            >
              Accept{modeValue && modeValue !== '-' ? ` (${modeValue})` : ''}
            </Button>
            <Dropdown
              menu={{
                items: overrideMenuItems,
                onClick: handleAcceptOverride,
              }}
            >
              <Button type="primary" icon={<DownOutlined />} />
            </Dropdown>
          </SpaceCompact>
        </>
      )}
    </div>
  )
}

export default VotingActions
