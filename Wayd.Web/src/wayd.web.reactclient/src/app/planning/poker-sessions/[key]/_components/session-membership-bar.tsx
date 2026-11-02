'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  PokerSessionDetailsDto,
  PokerSessionMemberDto,
  PokerSessionMemberMode,
} from '@/src/services/wayd-api'
import {
  useChangePokerSessionModeMutation,
  useJoinPokerSessionMutation,
  useLeavePokerSessionMutation,
} from '@/src/store/features/planning/poker-sessions-api'
import { Button, Flex, Segmented, Typography } from 'antd'
import { FC, useState } from 'react'

const { Text } = Typography

const modeOptions = [
  { label: 'Participant', value: PokerSessionMemberMode.Participant },
  { label: 'Observer', value: PokerSessionMemberMode.Observer },
]

export interface SessionMembershipBarProps {
  session: PokerSessionDetailsDto
  /** The caller's membership while they are in the session. */
  member: PokerSessionMemberDto | undefined
  /** False when the caller's account has no employee, which joining needs. */
  canJoin: boolean
}

/**
 * Joining an active session, then switching between Participant and Observer
 * or leaving it. Participant is the default; an Observer cannot vote and is
 * left out of the voted and waiting counts.
 */
const SessionMembershipBar: FC<SessionMembershipBarProps> = ({
  session,
  member,
  canJoin,
}) => {
  const messageApi = useMessage()
  const [joinMode, setJoinMode] = useState(PokerSessionMemberMode.Participant)
  const [join, { isLoading: isJoining }] = useJoinPokerSessionMutation()
  const [changeMode, { isLoading: isChanging }] =
    useChangePokerSessionModeMutation()
  const [leave, { isLoading: isLeaving }] = useLeavePokerSessionMutation()

  const handleJoin = async () => {
    const response = await join({
      id: session.id,
      key: session.key,
      mode: joinMode,
    })
    if (response.error) messageApi.error('Failed to join the session.')
  }

  const handleChangeMode = async (mode: PokerSessionMemberMode) => {
    const response = await changeMode({
      id: session.id,
      key: session.key,
      mode,
    })
    if (response.error) messageApi.error('Failed to change your mode.')
  }

  const handleLeave = async () => {
    const response = await leave({ id: session.id, key: session.key })
    if (response.error) messageApi.error('Failed to leave the session.')
  }

  if (!canJoin) {
    return (
      <Text type="secondary">
        Your account is not linked to an employee, so you can watch but not join
        this session.
      </Text>
    )
  }

  if (!member) {
    return (
      <Flex align="center" gap={8} wrap>
        <Text>Join as</Text>
        <Segmented
          size="small"
          options={modeOptions}
          value={joinMode}
          onChange={setJoinMode}
          aria-label="Join as"
        />
        <Button type="primary" onClick={handleJoin} loading={isJoining}>
          Join Session
        </Button>
      </Flex>
    )
  }

  return (
    <Flex align="center" gap={8} wrap>
      <Segmented
        size="small"
        options={modeOptions}
        value={member.mode}
        onChange={handleChangeMode}
        disabled={isChanging}
        aria-label="Your mode"
      />
      <Button size="small" onClick={handleLeave} loading={isLeaving}>
        Leave
      </Button>
    </Flex>
  )
}

export default SessionMembershipBar
