'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProductDto } from '@/src/services/wayd-api'
import { useDeleteProductMutation } from '@/src/store/features/product-management/products-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Form, Modal, Typography } from 'antd'

export interface DeleteProductFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Confirms deleting a product.
 *
 * The API refuses a product that still has children or releases, so the failure
 * message is shown rather than the button being pre-disabled: this page does not
 * load either count, and guessing would either block a valid delete or promise
 * one that will fail.
 */
const DeleteProductForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: DeleteProductFormProps) => {
  const messageApi = useMessage()

  const [deleteProduct] = useDeleteProductMutation()

  const { form, isOpen, isSaving, handleOk, handleCancel } = useModalForm({
    onSubmit: async () => {
      try {
        const response = await deleteProduct(product.id)
        if (response.error) throw response.error

        messageApi.success('Product deleted successfully.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while deleting the product. Please try again.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while deleting the product. Please try again.',
    permission: 'Permissions.Products.Delete',
  })

  return (
    <Modal
      title="Delete Product"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="delete-product-form">
        <Typography.Text>
          Delete <strong>{product.name}</strong>? This cannot be undone.
        </Typography.Text>
      </Form>
    </Modal>
  )
}

export default DeleteProductForm
