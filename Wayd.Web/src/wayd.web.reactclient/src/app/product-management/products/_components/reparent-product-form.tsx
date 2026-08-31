'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProductDto, ReparentProductRequest } from '@/src/services/wayd-api'
import {
  useGetProductsQuery,
  useReparentProductMutation,
} from '@/src/store/features/product-management/products-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal, Select } from 'antd'

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
 * The product itself is excluded from the options, but its descendants are not:
 * this form holds only the flat list, so it cannot see who sits beneath it. The
 * API walks the ancestry and refuses a move into a product's own subtree, and
 * that refusal is what the caller sees.
 */
const ReparentProductForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: ReparentProductFormProps) => {
  const messageApi = useMessage()

  const [reparentProduct] = useReparentProductMutation()
  const { data: products, isLoading } = useGetProductsQuery(undefined)

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

  const options = (products ?? [])
    .filter((candidate) => candidate.id !== product.id)
    .map((candidate) => ({ value: candidate.id, label: candidate.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

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
          <Select
            options={options}
            loading={isLoading}
            placeholder="Select a parent"
            allowClear
            showSearch
            optionFilterProp="label"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ReparentProductForm
