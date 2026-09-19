'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useDeleteProductTagCategoryMutation } from '@/src/store/features/product-management/product-tag-categories-api'
import { Alert, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'
import { ProductTagCategoryActionTarget } from './types'

export interface DeleteProductTagCategoryFormProps {
  category: ProductTagCategoryActionTarget
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Deletes an axis nothing is tagged along.
 *
 * The API refuses one that products are using, so the failure path here is a
 * real answer rather than a surprise — hence no client-side guess at whether
 * the delete will be allowed.
 */
const DeleteProductTagCategoryForm = ({
  category,
  onFormComplete,
  onFormCancel,
}: DeleteProductTagCategoryFormProps) => {
  const messageApi = useMessage()

  const [deleteProductTagCategory] = useDeleteProductTagCategoryMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteProductTagCategory(category.id)
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted tag category.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
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
    permission: 'Permissions.ProductTagCategories.Delete',
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

export default DeleteProductTagCategoryForm
