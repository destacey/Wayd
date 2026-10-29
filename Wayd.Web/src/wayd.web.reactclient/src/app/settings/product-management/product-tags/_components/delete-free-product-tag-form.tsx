'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ProductTagListItemDto } from '@/src/services/wayd-api'
import { useDeleteFreeProductTagMutation } from '@/src/store/features/product-management/product-tags-api'
import { Alert, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteFreeProductTagFormProps {
  tag: ProductTagListItemDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Deletes a free tag, taking it off every product that carries it. Unlike a tag
 * on a category, a free tag in use is not refused: free tags have no inactive
 * state to retire one to.
 */
const DeleteFreeProductTagForm = ({
  tag,
  onFormComplete,
  onFormCancel,
}: DeleteFreeProductTagFormProps) => {
  const messageApi = useMessage()

  const [deleteFreeProductTag] = useDeleteFreeProductTagMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteFreeProductTag(tag.id)
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted tag.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the tag.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: 'An unexpected error occurred while deleting the tag.',
    permission: 'Permissions.ProductTagCategories.Delete',
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
          tag.productCount > 0
            ? `It will be removed from ${tag.productCount} product(s). This cannot be undone.`
            : 'This cannot be undone.'
        }
      />
      <Typography.Paragraph
        type="secondary"
        style={{ marginTop: 12, marginBottom: 0 }}
      >
        Merge it instead to keep those products filed under another tag.
      </Typography.Paragraph>
    </Modal>
  )
}

export default DeleteFreeProductTagForm
