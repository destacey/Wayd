'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeRequestTagListItemDto } from '@/src/services/wayd-api'
import { useDeleteFreeIntakeRequestTagMutation } from '@/src/store/features/demand-management/demand-management-api'
import { Alert, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { toApiError } from '@/src/utils'

export interface DeleteFreeTagFormProps {
  tag: IntakeRequestTagListItemDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Deletes a free tag, taking it off every request that carries it. Unlike a tag
 * on an axis, a free tag in use is not refused: it has no inactive state to
 * retire to.
 */
const DeleteFreeTagForm = ({
  tag,
  onFormComplete,
  onFormCancel,
}: DeleteFreeTagFormProps) => {
  const messageApi = useMessage()

  const [deleteFreeTag] = useDeleteFreeIntakeRequestTagMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await deleteFreeTag(tag.id).unwrap()
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
      title="Delete Free Tag"
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
        Delete free tag <Typography.Text strong>{tag.name}</Typography.Text>?
      </Typography.Paragraph>
      <Alert
        type="warning"
        showIcon
        title={
          tag.requestCount > 0
            ? `It will be removed from ${tag.requestCount} request(s). This cannot be undone.`
            : 'This cannot be undone'
        }
      />
      <Typography.Paragraph
        type="secondary"
        style={{ marginTop: 12, marginBottom: 0 }}
      >
        To keep those requests filed under another tag, merge it instead.
      </Typography.Paragraph>
    </Modal>
  )
}

export default DeleteFreeTagForm
