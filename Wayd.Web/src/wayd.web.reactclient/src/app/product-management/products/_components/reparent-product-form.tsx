'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProductDto, ReparentProductRequest } from '@/src/services/wayd-api'
import { useReparentProductMutation } from '@/src/store/features/product-management/products-api'
import { ProductTreeSelect } from '../../_components'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal } from 'antd'

const { Item } = Form

export interface ReparentProductFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ReparentProductFormValues {
  parentId?: string
}

/**
 * Moves a product to a different parent, or to the root.
 *
 * The picker is a tree so the hierarchy being moved within is visible, and it
 * omits the product's own subtree: a product cannot become its own ancestor. The
 * API enforces that too, so this is about not offering the move rather than
 * about stopping it.
 */
const ReparentProductForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: ReparentProductFormProps) => {
  const messageApi = useMessage()

  const [reparentProduct] = useReparentProductMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ReparentProductFormValues>({
      onSubmit: async (values: ReparentProductFormValues, form) => {
        try {
          const request = {
            id: product.id,
            parentId: values.parentId,
          } as ReparentProductRequest

          const response = await reparentProduct({ id: product.id, request })
          if (response.error) throw response.error

          messageApi.success('Product moved successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while moving the product. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while moving the product. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  return (
    <Modal
      title="Move Product"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Move"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="reparent-product-form"
        initialValues={{ parentId: product.parent?.id }}
      >
        <Item
          name="parentId"
          label="Parent"
          extra="Clear this to make it a root product."
        >
          <ProductTreeSelect
            excludeSubtreeOf={product.id}
            placeholder="Select a parent"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ReparentProductForm
