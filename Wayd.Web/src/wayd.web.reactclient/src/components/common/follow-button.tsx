'use client'

import { EyeFilled, EyeOutlined } from '@ant-design/icons'
import { Button, Tooltip } from 'antd'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useFeatureFlag } from '@/src/hooks'
import {
  FollowOwnerType,
  useFollowMutation,
  useGetFollowStatusQuery,
  useUnfollowMutation,
} from '@/src/store/features/common/follows-api'
import { toApiError } from '@/src/utils/problem-details'

export interface FollowButtonProps {
  ownerType: FollowOwnerType
  ownerId: string
}

/**
 * Follows or stops following a record for the signed-in person.
 *
 * Shown only with the `following` flag on and an account linked to an
 * employee, since a follow belongs to an employee. Following grants nothing:
 * the button appears only on a record the person can already read, and the
 * server refuses a follow of anything else.
 *
 * Rendered by `RecordHeader` from its `follow` prop, at the end of the header
 * beside the details toggle, so every record places it the same way.
 */
const FollowButton = ({ ownerType, ownerId }: FollowButtonProps) => {
  const { isEnabled } = useFeatureFlag('following')
  const { user } = useAuth()
  const messageApi = useMessage()

  const canFollow = isEnabled && !!user?.employeeId && !!ownerId

  const {
    data: status,
    isLoading,
    error,
  } = useGetFollowStatusQuery({ ownerType, ownerId }, { skip: !canFollow })
  const [follow, { isLoading: isFollowing }] = useFollowMutation()
  const [unfollow, { isLoading: isUnfollowing }] = useUnfollowMutation()

  // A 404 means the record cannot be read, so it cannot be followed. Any other
  // failure leaves the button in place, disabled, rather than hiding it as if
  // the record were unreadable.
  if (!canFollow || (error && toApiError(error).status === 404)) return null

  const isUnavailable = !status
  const isPending = isFollowing || isUnfollowing
  const isFollowed = status?.isFollowing ?? false

  const onClick = async () => {
    const target = { ownerType, ownerId }
    const response = isFollowed ? await unfollow(target) : await follow(target)

    if ('error' in response) {
      messageApi.error(
        toApiError(response.error).detail ??
          (isFollowed
            ? 'Could not stop following this record.'
            : 'Could not follow this record.'),
      )
    }
  }

  // One stable Tooltip and Button whatever the state: swapping the trigger
  // node would make the tooltip flicker.
  return (
    <Tooltip
      title={
        isUnavailable
          ? isLoading
            ? 'Follow'
            : 'Could not load whether you follow this.'
          : isFollowed
            ? 'You follow this. Click to stop following.'
            : 'Follow this to keep it in your Following list.'
      }
    >
      {/* Icon only: a toggle keeps one name, and aria-pressed says whether
          it is on. Borderless, and tinted primary while on, like the details
          toggle beside it: it marks a state rather than offering an action. */}
      <Button
        type="text"
        icon={
          isFollowed ? (
            <EyeFilled style={{ color: 'var(--ant-color-primary)' }} />
          ) : (
            <EyeOutlined />
          )
        }
        aria-label="Follow"
        aria-pressed={isFollowed}
        disabled={isUnavailable || isPending}
        onClick={onClick}
      />
    </Tooltip>
  )
}

export default FollowButton
