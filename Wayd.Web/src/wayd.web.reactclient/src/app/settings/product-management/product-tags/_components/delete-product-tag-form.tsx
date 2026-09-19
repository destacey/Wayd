'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ProductTagOptionDto } from '@/src/services/wayd-api'
import { useDeleteProductTagMutation } from '@/src/store/features/product-management/product-tag-categories-api'
import { Alert, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteProductTagFormProps {
  categoryId: string
  tag: ProductTagOptionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/** Deletes a tag no product carries. The API refuses one in use. */
const DeleteProductTagForm = ({
  categoryId,
  tag,
  onFormComplete,
  onFormCancel,
}: DeleteProductTagFormProps) => {
  const messageApi = useMessage()

  const [deleteProductTag] = useDeleteProductTagMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteProductTag({ categoryId, tagId: tag.id })
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

export default DeleteProductTagForm
