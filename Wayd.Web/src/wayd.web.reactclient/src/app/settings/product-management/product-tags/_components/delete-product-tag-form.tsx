'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ProductTagOptionDto } from '@/src/services/wayd-api'
import { useDeleteProductTagMutation } from '@/src/store/features/product-management/product-tag-categories-api'
import { Modal } from 'antd'
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
      title="Are you sure you want to delete this tag?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {tag.name}
      <p>A tag products carry cannot be deleted. Deactivate it instead.</p>
    </Modal>
  )
}

export default DeleteProductTagForm
