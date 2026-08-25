'use client'

import { Avatar, Flex } from 'antd'
import PersonPopover from './person-popover'

const { Group: AvatarGroup } = Avatar

/**
 * Past this the row is unreadable anyway, and the overflow popover is a better
 * home for the tail than a line of avatars nobody can tell apart. High enough
 * that a real session or map never reaches it.
 */
const MAX_AVATARS = 25

export interface PresenceParticipantSummary {
  id: string
  name: string
  /** Absent when the account is not linked to an employee. */
  employeeId?: string
}

export interface PresenceAvatarsProps {
  participants: PresenceParticipantSummary[]
}

/**
 * Who is currently in a session, a map, a shared surface.
 *
 * A row of its own rather than a slot in the identity bar, which cannot spare
 * the width for more than a couple of avatars before pushing the record's own
 * actions off the line.
 */
const PresenceAvatars = ({ participants }: PresenceAvatarsProps) => {
  if (participants.length === 0) return null

  return (
    // Right-aligned so the group grows leftward into the row's free space.
    <Flex align="center" justify="flex-end" gap={10} wrap>
      <AvatarGroup
        size="small"
        // AvatarGroup is inline-flex with no wrap of its own, so a large group
        // would overflow the row rather than fold onto a second line.
        style={{ flexWrap: 'wrap', rowGap: 6 }}
        max={{
          count: MAX_AVATARS,
          style: { fontSize: 12 },
          // Click, not the default hover: each avatar inside the overflow opens
          // its own card on click, and a hover-triggered overflow would close
          // the moment the pointer left it, taking the card with it.
          popover: { trigger: 'click' },
        }}
      >
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

export default PresenceAvatars
