'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  PokerSessionDetailsDto,
  PokerSessionMemberDto,
} from '@/src/services/wayd-api'
import {
  useJoinPokerSessionMutation,
  useSetPokerSessionVotingMutation,
} from '@/src/store/features/planning/poker-sessions-api'
import { Button, Flex, Switch, Tooltip, Typography } from 'antd'
import { FC, useEffect, useRef } from 'react'

const { Text } = Typography

export interface SessionMembershipBarProps {
  session: PokerSessionDetailsDto
  /** The caller's membership once they have joined. */
  member: PokerSessionMemberDto | undefined
  /** False when the caller's account has no employee, which joining needs. */
  canJoin: boolean
}

/**
 * The caller's place in an active session. Opening the session joins them,
 * voting; the switch turns voting off for someone who takes part without
 * estimating, such as a product manager answering the team's questions.
 */
const SessionMembershipBar: FC<SessionMembershipBarProps> = ({
  session,
  member,
  canJoin,
}) => {
  const messageApi = useMessage()
  const [join, { isLoading: isJoining }] = useJoinPokerSessionMutation()
  const [setVoting, { isLoading: isSettingVoting }] =
    useSetPokerSessionVotingMutation()
  // The session this visit has been in or tried to join, so a failed join or
  // a second effect run in development does not join again.
  const joinedSessionId = useRef<string | undefined>(undefined)

  const handleJoin = async () => {
    const response = await join({ id: session.id, key: session.key })
    if (response.error) messageApi.error('Failed to join the session.')
  }

  useEffect(() => {
    if (!canJoin || joinedSessionId.current === session.id) return
    joinedSessionId.current = session.id
    if (member) return
    join({ id: session.id, key: session.key }).then((response) => {
      if (response.error) messageApi.error('Failed to join the session.')
    })
  }, [canJoin, member, session.id, session.key, join, messageApi])

  const handleVotingChange = async (isVoting: boolean) => {
    const response = await setVoting({
      id: session.id,
      key: session.key,
      isVoting,
    })
    if (response.error) messageApi.error('Failed to change your voting.')
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
    return isJoining ? (
      <Text type="secondary">Joining…</Text>
    ) : (
      <Flex align="center" gap={8} wrap>
        <Text type="secondary">You are not in this session.</Text>
        <Button type="primary" size="small" onClick={handleJoin}>
          Join Session
        </Button>
      </Flex>
    )
  }

  return (
    <Tooltip title="Turn off if you won't estimate — you still take part in the discussion.">
      <Flex align="center" gap={8}>
        <Switch
          size="small"
          checked={member.isVoting}
          onChange={handleVotingChange}
          loading={isSettingVoting}
          aria-label="Voting"
        />
        <Text>Voting</Text>
      </Flex>
    </Tooltip>
  )
}

export default SessionMembershipBar
