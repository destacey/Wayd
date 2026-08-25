'use client'

import { PersonPopover } from '@/src/components/common'
import { PresenceParticipant } from '@/src/hooks/use-poker-session-connection'
import { Avatar, Flex } from 'antd'

const { Group: AvatarGroup } = Avatar

export interface ConnectedParticipantsProps {
  participants: PresenceParticipant[]
}

/**
 * Who is currently in the session.
 *
 * A row of its own above the session rather than a slot in the identity bar,
 * which could not spare the width for more than a couple of avatars.
 */
const ConnectedParticipants = ({ participants }: ConnectedParticipantsProps) => {
  if (participants.length === 0) return null

  return (
    // Right-aligned, so the group grows leftward into the row's free space
    // instead of drifting away from the sidebar it sits above.
    <Flex align="center" justify="flex-end" gap={10} wrap>
      <AvatarGroup max={{ count: 12, style: { fontSize: 12 } }} size="small">
        {participants.map((p) => (
          <PersonPopover
            key={p.id}
            name={p.name}
            employeeId={p.employeeId}
            colorKey={p.id}
          />
        ))}
      </AvatarGroup>
    </Flex>
  )
}

export default ConnectedParticipants
