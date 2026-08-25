'use client'

import { getAvatarColor, getInitials } from '@/src/utils'
import { Avatar, AvatarProps } from 'antd'
import WaydTooltip from './wayd-tooltip'
import styles from './person-avatar.module.css'

export interface PersonAvatarProps extends Omit<AvatarProps, 'children'> {
  /** The person's display name — "Daniel Stacey". Names the tooltip too. */
  name: string
  /**
   * Colour seed. Defaults to the name, so the same person keeps the same
   * colour; pass a stable id where one is available and names may repeat.
   */
  colorKey?: string
  /** Omit where the name is already beside the avatar. */
  showTooltip?: boolean
  /** Hover text, where the name alone is not the whole story. Defaults to it. */
  tooltip?: string
}

/**
 * A person as a coloured avatar of their initials.
 *
 * Two initials rather than one: a room of people is only legible at a glance
 * if the avatars can be told apart, and first-initial-only collides as soon as
 * two names share a letter.
 *
 * The hover raises the avatar clear of any overlapping it, which is what makes
 * a stacked group readable — the tooltip names one person, and lifting it above
 * its neighbours shows which one is being named.
 */
const PersonAvatar = ({
  name,
  colorKey,
  showTooltip = true,
  tooltip,
  size = 'small',
  className,
  style,
  ...rest
}: PersonAvatarProps) => {
  const avatar = (
    <Avatar
      size={size}
      className={`${styles.avatar} ${className ?? ''}`}
      style={{ backgroundColor: getAvatarColor(colorKey ?? name), ...style }}
      {...rest}
    >
      {getInitials(name)}
    </Avatar>
  )

  if (!showTooltip) return avatar

  return <WaydTooltip title={tooltip ?? name}>{avatar}</WaydTooltip>
}

export default PersonAvatar
