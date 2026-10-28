'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useDeleteIntakeRequestTagCategoryMutation } from '@/src/store/features/demand-management/demand-management-api'
import { Alert, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { toApiError } from '@/src/utils'
import { TagAxisActionTarget } from './types'

export interface DeleteTagAxisFormProps {
  axis: TagAxisActionTarget
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Deletes a tag axis no request is tagged along. The API refuses one in use —
 * including requests on a private desk the viewer cannot count — so its
 * answer is shown rather than guessed at.
 */
const DeleteTagAxisForm = ({
  axis,
  onFormComplete,
  onFormCancel,
}: DeleteTagAxisFormProps) => {
  const messageApi = useMessage()

  const [deleteTagAxis] = useDeleteIntakeRequestTagCategoryMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await deleteTagAxis(axis.id).unwrap()
        messageApi.success('Tag axis deleted.')
        return true
      } catch (error) {
        messageApi.error(
          toApiError(error).detail ??
            'An unexpected error occurred while deleting the tag axis.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: 'An unexpected error occurred while deleting the tag axis.',
    permission: 'Permissions.IntakeRequests.Administer',
  })

  return (
    <Modal
      title="Delete Tag Axis"
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
        Delete tag axis <Typography.Text strong>{axis.name}</Typography.Text>?
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

export default DeleteTagAxisForm
