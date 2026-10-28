'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useSetIntakeRequestTagCategoryActiveMutation } from '@/src/store/features/demand-management/demand-management-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { toApiError } from '@/src/utils'
import { TagAxisActionTarget } from './types'

export interface ChangeTagAxisActiveFormProps {
  axis: TagAxisActionTarget
  /** True activates, false deactivates. */
  isActive: boolean
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Takes a tag axis out of use, or puts it back.
 *
 * Deactivation is how an axis normally retires: requests already tagged along
 * it keep their tags, and the confirmation says so, because "deactivate" on its
 * own reads as if it might strip them.
 */
const ChangeTagAxisActiveForm = ({
  axis,
  isActive,
  onFormComplete,
  onFormCancel,
}: ChangeTagAxisActiveFormProps) => {
  const messageApi = useMessage()

  const [setTagAxisActive] = useSetIntakeRequestTagCategoryActiveMutation()

  const gerund = isActive ? 'activating' : 'deactivating'

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await setTagAxisActive({ id: axis.id, isActive }).unwrap()
        messageApi.success(
          isActive ? 'Tag axis activated.' : 'Tag axis deactivated.',
        )
        return true
      } catch (error) {
        messageApi.error(
          toApiError(error).detail ??
            `An unexpected error occurred while ${gerund} the tag axis.`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${gerund} the tag axis.`,
    permission: 'Permissions.IntakeRequests.Administer',
  })

  return (
    <Modal
      title={`${isActive ? 'Activate' : 'Deactivate'} this tag axis?`}
      open={isOpen}
      onOk={handleOk}
      okText={isActive ? 'Activate' : 'Deactivate'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {axis.name}
      {!isActive && (
        <p>
          Requests can no longer be tagged along this axis. Requests already
          tagged keep their tags.
        </p>
      )}
    </Modal>
  )
}

export default ChangeTagAxisActiveForm
