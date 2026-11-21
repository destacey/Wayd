'use client'

import { formatDateTime } from '@/src/components/common/wayd-grid'
import { instantTime, type Instant } from '@/src/utils/instant'
import { Tag, Tooltip } from 'antd'

export interface StagedLinkTagProps {
  /** What is staged, as the tag names it: "Tenant migration", "Provider migration". */
  label: string
  /** When the staged link stops being honoured; null for one staged before its time was recorded. */
  expiresAt?: Instant | null
  /** The current time, as epoch milliseconds. */
  now: number
}

/**
 * Whether a staged link can still be completed by the user's sign-in. One with no expiry was staged before its time
 * was recorded, and the API treats it as expired.
 */
export const isStagedLinkOpen = (
  expiresAt: Instant | null | undefined,
  now: number,
) => !!expiresAt && instantTime(expiresAt) >= now

/**
 * A staged link in the user's identity bar: pending until it expires, then marked expired, since the user's sign-in
 * no longer completes it and an administrator must stage it again.
 */
const StagedLinkTag = ({ label, expiresAt, now }: StagedLinkTagProps) =>
  isStagedLinkOpen(expiresAt, now) ? (
    <Tooltip title={`Expires ${formatDateTime(expiresAt)}`}>
      <Tag color="processing">{label} pending</Tag>
    </Tooltip>
  ) : (
    <Tooltip title="The user didn't sign in within 14 days of it being staged. Stage it again to give them another 14 days.">
      <Tag color="warning">{label} expired</Tag>
    </Tooltip>
  )

export default StagedLinkTag
