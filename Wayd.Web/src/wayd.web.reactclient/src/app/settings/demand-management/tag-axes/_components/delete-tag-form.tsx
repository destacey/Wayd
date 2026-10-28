'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeRequestTagOptionDto } from '@/src/services/wayd-api'
import { useDeleteIntakeRequestTagMutation } from '@/src/store/features/demand-management/demand-management-api'
import { Alert, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { toApiError } from '@/src/utils'

export interface DeleteTagFormProps {
  axisId: string
  tag: IntakeRequestTagOptionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/** Deletes a tag no request carries. The API refuses one in use. */
const DeleteTagForm = ({
  axisId,
  tag,
  onFormComplete,
  onFormCancel,
}: DeleteTagFormProps) => {
  const messageApi = useMessage()

  const [deleteTag] = useDeleteIntakeRequestTagMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await deleteTag({ categoryId: axisId, tagId: tag.id }).unwrap()
        messageApi.success('Tag deleted.')
        return true
      } catch (error) {
        messageApi.error(
          toApiError(error).detail ??
            'An unexpected error occurred while deleting the tag.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: 'An unexpected error occurred while deleting the tag.',
    permission: 'Permissions.IntakeRequests.Administer',
  })

  return (
    <Modal
      title="Delete Tag"
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
        Delete tag <Typography.Text strong>{tag.name}</Typography.Text>?
      </Typography.Paragraph>
      <Alert type="warning" showIcon title="This cannot be undone" />
      <Typography.Paragraph
        type="secondary"
        style={{ marginTop: 12, marginBottom: 0 }}
      >
        Deactivate it instead to keep it.
      </Typography.Paragraph>
    </Modal>
  )
}

export default DeleteTagForm
