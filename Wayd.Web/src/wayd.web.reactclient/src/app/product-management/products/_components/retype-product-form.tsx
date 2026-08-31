'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProductDto, RetypeProductRequest } from '@/src/services/wayd-api'
import { useRetypeProductMutation } from '@/src/store/features/product-management/products-api'
import { useGetProductTypesQuery } from '@/src/store/features/product-management/product-types-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal, Select } from 'antd'

const { Item } = Form

export interface RetypeProductFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface RetypeProductFormValues {
  productTypeId: string
}

/**
 * Changes what kind of thing a product is.
 *
 * The API refuses a move onto a non-releasable type once releases exist, so the
 * failure is reported rather than the option being hidden: this form does not
 * load the release count, and guessing would either block a valid change or
 * promise one that will fail.
 */
const RetypeProductForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: RetypeProductFormProps) => {
  const messageApi = useMessage()

  const [retypeProduct] = useRetypeProductMutation()
  // Active types only, matching what the API accepts for a change.
  const { data: productTypes, isLoading } = useGetProductTypesQuery(true)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<RetypeProductFormValues>({
      onSubmit: async (values: RetypeProductFormValues, form) => {
        try {
          const request = {
            id: product.id,
            productTypeId: values.productTypeId,
          } as RetypeProductRequest

          const response = await retypeProduct({ id: product.id, request })
          if (response.error) throw response.error

          messageApi.success('Product type changed successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while changing the type. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while changing the type. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  const options = (productTypes ?? [])
    .map((type) => ({
      value: type.id,
      label: type.isReleasable ? type.name : `${type.name} (not releasable)`,
    }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  return (
    <Modal
      title="Change Type"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Change"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="retype-product-form"
        initialValues={{ productTypeId: product.type?.id }}
      >
        <Item
          label="Type"
          name="productTypeId"
          rules={[{ required: true, message: 'Type is required' }]}
          extra="Decides whether releases can be cut against this product. A product with releases cannot move to a type that has none."
        >
          <Select
            options={options}
            loading={isLoading}
            placeholder="Select a type"
            showSearch
            optionFilterProp="label"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default RetypeProductForm
