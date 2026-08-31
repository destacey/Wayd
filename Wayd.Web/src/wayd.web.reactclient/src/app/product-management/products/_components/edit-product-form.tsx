'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProductDto, UpdateProductRequest } from '@/src/services/wayd-api'
import { useUpdateProductMutation } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect } from 'react'

const { Item } = Form

export interface EditProductFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditProductFormValues {
  name: string
  description?: string
}

/**
 * Edits a product's descriptive fields.
 *
 * Type, parent, status and the external link are changed through their own
 * actions: each carries a rule the API enforces or a different intent, and
 * folding them in here would hide which one refused.
 */
const EditProductForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: EditProductFormProps) => {
  const messageApi = useMessage()

  const [updateProduct] = useUpdateProductMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditProductFormValues>({
      onSubmit: async (values: EditProductFormValues, form) => {
        try {
          // Whole-record update, matching the API's PUT semantics: an omitted
          // optional field is cleared rather than left as it was.
          const request = {
            id: product.id,
            name: values.name,
            description: values.description,
          } as UpdateProductRequest

          const response = await updateProduct({ id: product.id, request })
          if (response.error) throw response.error

          messageApi.success('Product updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the product. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the product. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  useEffect(() => {
    form.setFieldsValue({
      name: product.name,
      description: product.description,
    })
  }, [product, form])

  return (
    <Modal
      title="Edit Product"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="edit-product-form">
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 128 }]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <MarkdownEditor maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditProductForm
