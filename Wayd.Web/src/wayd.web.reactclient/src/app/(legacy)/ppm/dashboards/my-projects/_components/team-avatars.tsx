'use client'

import { getAvatarColor } from '@/src/utils'
import { Avatar } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import { FC } from 'react'
import { getInitials, TeamMemberWithRoles } from './project-card-helpers'

const { Group: AvatarGroup } = Avatar

const MAX_AVATARS = 6

const TeamAvatars: FC<{ members: TeamMemberWithRoles[] }> = ({ members }) => {
  const visible = members.slice(0, MAX_AVATARS)
  const overflow = members.length - MAX_AVATARS

  return (
    <AvatarGroup size="small">
      {visible.map(({ employee, roles }) => (
        <WaydTooltip
          key={employee.id}
          title={`${employee.name} (${roles.join(', ')})`}
        >
          <Avatar
            size="small"
            style={{
              backgroundColor: getAvatarColor(employee.id),
              fontSize: 10,
              fontWeight: 600,
            }}
          >
            {getInitials(employee.name)}
          </Avatar>
        </WaydTooltip>
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

