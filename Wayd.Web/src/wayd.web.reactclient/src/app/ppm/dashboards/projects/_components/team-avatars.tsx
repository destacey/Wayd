'use client'

import { Avatar } from 'antd'
import { PersonPopover, WaydTooltip } from '@/src/components/common'
import { FC } from 'react'
import { TeamMemberWithRoles } from './dashboard-model'

const { Group: AvatarGroup } = Avatar

const DEFAULT_MAX_AVATARS = 6

export interface TeamAvatarsProps {
  members: TeamMemberWithRoles[]
  max?: number
}

const TeamAvatars: FC<TeamAvatarsProps> = ({
  members,
  max = DEFAULT_MAX_AVATARS,
}) => {
  const visible = members.slice(0, max)
  const hidden = members.slice(max)
  const overflow = hidden.length

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
        <WaydTooltip
          title={hidden
            .map(
              ({ employee, roles }) => `${employee.name} (${roles.join(', ')})`,
            )
            .join(', ')}
        >
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
        </WaydTooltip>
      )}
    </AvatarGroup>
  )
}

export default TeamAvatars
