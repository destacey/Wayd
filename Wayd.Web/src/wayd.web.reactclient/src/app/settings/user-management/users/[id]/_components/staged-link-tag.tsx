'use client'

import { formatDateTime } from '@/src/components/common/wayd-grid'
import type { Instant } from '@/src/utils/instant'
import { isStagedLinkOpen } from '@/src/utils/staged-link'
import { Tag, Tooltip } from 'antd'

export interface StagedLinkTagProps {
  /** What is staged, as the tag names it: "First sign-in", "Tenant migration", "Provider migration". */
  label: string
  /** When the staged link stops being honoured; null for one staged before its time was recorded. */
  expiresAt?: Instant | null
  /** The current time, as epoch milliseconds. */
  now: number
  /** How an administrator stages this link again, shown once it has expired. */
  renewal: string
}

/**
 * A staged link in the user's identity bar: pending until it expires, then marked expired, since the user's sign-in
 * no longer completes it and an administrator must stage it again.
 */
const StagedLinkTag = ({
  label,
  expiresAt,
  now,
  renewal,
}: StagedLinkTagProps) =>
  expiresAt && isStagedLinkOpen(expiresAt, now) ? (
    <Tooltip title={`Expires ${formatDateTime(expiresAt)}`}>
      <Tag color="processing">{label} pending</Tag>
    </Tooltip>
  ) : (
    <Tooltip
      title={`The user didn't sign in within 14 days of it being staged. ${renewal}`}
    >
      <Tag color="warning">{label} expired</Tag>
    </Tooltip>
  )

export default StagedLinkTag
