'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useDeleteIntakeRequestTagCategoryMutation } from '@/src/store/features/demand-management/demand-management-api'
import { Alert, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { toApiError } from '@/src/utils'
import { TagCategoryActionTarget } from './types'

export interface DeleteTagCategoryFormProps {
  category: TagCategoryActionTarget
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Deletes a tag category no request is tagged in. The API refuses one in use —
 * including requests on a private desk the viewer cannot count — so its
 * answer is shown rather than guessed at.
 */
const DeleteTagCategoryForm = ({
  category,
  onFormComplete,
  onFormCancel,
}: DeleteTagCategoryFormProps) => {
  const messageApi = useMessage()

  const [deleteTagCategory] = useDeleteIntakeRequestTagCategoryMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await deleteTagCategory(category.id).unwrap()
        messageApi.success('Tag category deleted.')
        return true
      } catch (error) {
        messageApi.error(
          toApiError(error).detail ??
            'An unexpected error occurred while deleting the tag category.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the tag category.',
    permission: 'Permissions.IntakeRequests.Administer',
  })

  return (
    <Modal
      title="Delete Tag Category"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Typography.Paragraph>
        Delete tag category{' '}
        <Typography.Text strong>{category.name}</Typography.Text>?
      </Typography.Paragraph>
      <Alert
        type="warning"
        showIcon
        title="This cannot be undone"
        description="Its tags are deleted too."
      />
      <Typography.Paragraph
        type="secondary"
        style={{ marginTop: 12, marginBottom: 0 }}
      >
        Deactivate it instead to keep it.
      </Typography.Paragraph>
    </Modal>
  )
}

export default DeleteTagCategoryForm
