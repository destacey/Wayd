'use client'

import { Avatar, theme } from 'antd'

export interface RecordAvatarProps {
  /** The person's initials, from `personInitials`. */
  initials?: string
  /** Photo URL; Avatar falls back to the initials if it fails to load. */
  src?: string
}

/**
 * The circle of initials beside a person's name.
 *
 * People only — employees and user accounts are the whole of it. Other records
 * carry no glyph: an entity icon identifies the *type* the page has already
 * named rather than the record, and initials on a non-person both read as a
 * person and collide ("Identity Platform" and "Infrastructure Program" are
 * both IP). Uniqueness lives in the key chip instead.
 */
const RecordAvatar = ({ initials, src }: RecordAvatarProps) => {
  const { token } = theme.useToken()

  return (
    <Avatar
      shape="circle"
      // Smaller on xs so the identity line still fits a phone width.
      size={{ xs: 28, sm: 32, md: 32, lg: 32, xl: 32, xxl: 32 }}
      src={src}
      style={{
        background: token.colorPrimaryBg,
        border: `1px solid ${token.colorPrimaryBorder}`,
        color: token.colorPrimaryText,
        flexShrink: 0,
      }}
      data-testid="record-avatar-person"
    >
      {initials}
    </Avatar>
  )
}

export default RecordAvatar
