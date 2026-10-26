'use client'

import { MutedFilled, MutedOutlined } from '@ant-design/icons'
import { Button, Tooltip } from 'antd'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useFeatureFlag } from '@/src/hooks'
import { FollowOwnerType } from '@/src/store/features/common/follows-api'
import {
  useGetRecordMuteStatusQuery,
  useMuteRecordMutation,
  useUnmuteRecordMutation,
} from '@/src/store/features/common/notification-mutes-api'
import { toApiError } from '@/src/utils/problem-details'

export interface MuteButtonProps {
  ownerType: FollowOwnerType
  ownerId: string
}

/**
 * Mutes or unmutes a record's notifications for the signed-in person.
 *
 * Sits beside the Follow button and is shown whether or not the person follows
 * the record, since people who take part in a record are told about it too.
 * Muting changes nothing else: the person keeps their access, roles and follow.
 */
const MuteButton = ({ ownerType, ownerId }: MuteButtonProps) => {
  const { isEnabled } = useFeatureFlag('following')
  const { user } = useAuth()
  const messageApi = useMessage()

  const canMute = isEnabled && !!user?.employeeId && !!ownerId

  const {
    data: status,
    isLoading,
    error,
  } = useGetRecordMuteStatusQuery({ ownerType, ownerId }, { skip: !canMute })
  const [mute, { isLoading: isMuting }] = useMuteRecordMutation()
  const [unmute, { isLoading: isUnmuting }] = useUnmuteRecordMutation()

  // A 404 means the record cannot be read, so it cannot be muted.
  if (!canMute || (error && toApiError(error).status === 404)) return null

  const isUnavailable = !status
  const isPending = isMuting || isUnmuting
  const isMuted = status?.isMuted ?? false

  const onClick = async () => {
    const target = { ownerType, ownerId }
    const response = isMuted ? await unmute(target) : await mute(target)

    if ('error' in response) {
      messageApi.error(
        toApiError(response.error).detail ??
          (isMuted
            ? 'Could not unmute this record.'
            : 'Could not mute this record.'),
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
            ? 'Mute notifications'
            : 'Could not load whether you muted this.'
          : isMuted
            ? 'Muted: you are told nothing about this. Click to unmute.'
            : 'Mute notifications about this. You keep your access and anything you follow.'
      }
    >
      <Button
        icon={isMuted ? <MutedFilled /> : <MutedOutlined />}
        aria-label="Mute notifications"
        aria-pressed={isMuted}
        disabled={isUnavailable || isPending}
        onClick={onClick}
      />
    </Tooltip>
  )
}

export default MuteButton
