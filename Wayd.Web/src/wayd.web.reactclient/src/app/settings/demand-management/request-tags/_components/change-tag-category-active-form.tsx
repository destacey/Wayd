'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useSetIntakeRequestTagCategoryActiveMutation } from '@/src/store/features/demand-management/demand-management-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { toApiError } from '@/src/utils'
import { TagCategoryActionTarget } from './types'

export interface ChangeTagCategoryActiveFormProps {
  category: TagCategoryActionTarget
  /** True activates, false deactivates. */
  isActive: boolean
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Takes a tag category out of use, or puts it back.
 *
 * Deactivation is how a category normally retires: requests already tagged in
 * it keep their tags, and the confirmation says so, because "deactivate" on its
 * own reads as if it might strip them.
 */
const ChangeTagCategoryActiveForm = ({
  category,
  isActive,
  onFormComplete,
  onFormCancel,
}: ChangeTagCategoryActiveFormProps) => {
  const messageApi = useMessage()

  const [setTagCategoryActive] = useSetIntakeRequestTagCategoryActiveMutation()

  const gerund = isActive ? 'activating' : 'deactivating'

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await setTagCategoryActive({ id: category.id, isActive }).unwrap()
        messageApi.success(
          isActive ? 'Tag category activated.' : 'Tag category deactivated.',
        )
        return true
      } catch (error) {
        messageApi.error(
          toApiError(error).detail ??
            `An unexpected error occurred while ${gerund} the tag category.`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${gerund} the tag category.`,
    permission: 'Permissions.IntakeRequests.Administer',
  })

  return (
    <Modal
      title={`${isActive ? 'Activate' : 'Deactivate'} this tag category?`}
      open={isOpen}
      onOk={handleOk}
      okText={isActive ? 'Activate' : 'Deactivate'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {category.name}
      {!isActive && (
        <p>
          Requests can no longer be tagged in this category. Requests already
          tagged keep their tags.
        </p>
      )}
    </Modal>
  )
}

export default ChangeTagCategoryActiveForm
