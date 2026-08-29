'use client'

import { Avatar } from 'antd'
import { PersonPopover } from '@/src/components/common'
import { FC } from 'react'
import { TeamMemberWithRoles } from './project-card-helpers'

const { Group: AvatarGroup } = Avatar

const MAX_AVATARS = 6

const TeamAvatars: FC<{ members: TeamMemberWithRoles[] }> = ({ members }) => {
  const visible = members.slice(0, MAX_AVATARS)
  const overflow = members.length - MAX_AVATARS

  return (
    <AvatarGroup size="small">
      {visible.map(({ employee, roles }) => (
        // The roles ride on the popover's own tooltip rather than a second one
        // over it — the roles are why this list exists, and they would
        // otherwise be lost behind the card.
        <PersonPopover
          key={employee.id}
          name={employee.name}
          tooltip={`${employee.name} (${roles.join(', ')})`}
          employeeId={employee.id}
          colorKey={employee.id}
        />
      ))}
      {overflow > 0 && (
        <Avatar
          size="small"
          style={{
            backgroundColor: 'var(--ant-color-fill-secondary)',
            color: 'var(--ant-color-text-secondary)',
            fontSize: 10,
            fontWeight: 600,
          }}
        >
          +{overflow}
        </Avatar>
      )}
    </AvatarGroup>
  )
}

export default TeamAvatars

