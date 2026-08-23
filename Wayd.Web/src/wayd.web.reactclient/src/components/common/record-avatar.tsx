import { Avatar, theme } from 'antd'
import { ReactNode } from 'react'

/**
 * A person gets a circle, every other record gets a rounded square. The shape
 * is the type signal — it reads before any text does, and initials on a
 * non-person both look like a person and collide ("Identity Platform" and
 * "Infrastructure Program" are both IP).
 */
export type RecordAvatarKind = 'person' | 'record'

export interface RecordAvatarProps {
  kind: RecordAvatarKind
  /** Initials for a person. Ignored when `kind` is 'record'. */
  initials?: string
  /** Entity icon for a record — the same one the sider uses for that type. */
  icon?: ReactNode
  /** Photo URL for a person; Avatar falls back to the initials if it fails. */
  src?: string
}

const RecordAvatar = ({ kind, initials, icon, src }: RecordAvatarProps) => {
  const { token } = theme.useToken()

  const isPerson = kind === 'person'

  return (
    <Avatar
      shape={isPerson ? 'circle' : 'square'}
      // Smaller on xs so the identity line still fits a phone width.
      size={{ xs: 28, sm: 32, md: 32, lg: 32, xl: 32, xxl: 32 }}
      src={isPerson ? src : undefined}
      icon={isPerson ? undefined : icon}
      style={{
        background: token.colorPrimaryBg,
        border: `1px solid ${token.colorPrimaryBorder}`,
        color: token.colorPrimaryText,
        flexShrink: 0,
      }}
      data-testid={isPerson ? 'record-avatar-person' : 'record-avatar-record'}
    >
      {isPerson ? initials : undefined}
    </Avatar>
  )
}

export default RecordAvatar
