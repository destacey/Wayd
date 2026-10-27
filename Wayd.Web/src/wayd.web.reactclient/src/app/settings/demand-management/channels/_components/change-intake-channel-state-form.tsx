'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeChannelDto } from '@/src/services/wayd-api'
import {
  useArchiveIntakeChannelMutation,
  useRestoreIntakeChannelMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface ChangeIntakeChannelStateFormProps {
  channel: IntakeChannelDto
  /** True archives the channel, false restores it. */
  archive: boolean
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Archives a channel or restores an archived one.
 *
 * Channels are archived, never deleted: requests refer to them, so an archived
 * channel stays on the requests that have it. The confirmation says so,
 * because "archive" on its own can read as if it strips them.
 */
const ChangeIntakeChannelStateForm = ({
  channel,
  archive,
  onFormComplete,
  onFormCancel,
}: ChangeIntakeChannelStateFormProps) => {
  const messageApi = useMessage()

  const [archiveChannel] = useArchiveIntakeChannelMutation()
  const [restoreChannel] = useRestoreIntakeChannelMutation()

  const gerund = archive ? 'archiving' : 'restoring'

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        if (archive) {
          await archiveChannel(channel.id).unwrap()
        } else {
          await restoreChannel(channel.id).unwrap()
        }
        messageApi.success(archive ? 'Channel archived.' : 'Channel restored.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${gerund} the channel.`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${gerund} the channel.`,
    permission: 'Permissions.IntakeRequests.Administer',
  })

  return (
    <Modal
      title={`${archive ? 'Archive' : 'Restore'} this channel?`}
      open={isOpen}
      onOk={handleOk}
      okText={archive ? 'Archive' : 'Restore'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {channel.name}
      {archive ? (
        <p>
          No desk will offer it and no new request can be given it. Requests
          already from this channel keep it.
        </p>
      ) : (
        <p>
          Desks that offer every channel, and desks that had chosen it, offer it
          again.
        </p>
      )}
    </Modal>
  )
}

export default ChangeIntakeChannelStateForm
