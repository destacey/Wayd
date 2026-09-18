'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ProductDependencyDto,
  RemoveProductDependencyRequest,
} from '@/src/services/wayd-api'
import { useRemoveProductDependencyMutation } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, Form, Input, Modal } from 'antd'

const { Item } = Form
const { TextArea } = Input

export interface RemoveProductDependencyFormProps {
  dependency: ProductDependencyDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface RemoveProductDependencyFormValues {
  reason?: string
}

/**
 * Deletes a dependency recorded by mistake.
 *
 * Asks for a reason, and points a dependency that merely stopped at End instead: removing one that was real
 * erases the record of when it held, which is what later attributes an outage upstream to this product.
 */
const RemoveProductDependencyForm = ({
  dependency,
  onFormComplete,
  onFormCancel,
}: RemoveProductDependencyFormProps) => {
  const messageApi = useMessage()

  const [removeProductDependency] = useRemoveProductDependencyMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<RemoveProductDependencyFormValues>({
      onSubmit: async (values: RemoveProductDependencyFormValues, form) => {
        try {
          const request = {
            reason: values.reason,
          } as RemoveProductDependencyRequest

          const response = await removeProductDependency({
            productId: dependency.product.id,
            dependencyId: dependency.id,
            dependsOnProductId: dependency.dependsOnProduct.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Dependency removed.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while removing the dependency. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while removing the dependency. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  return (
    <Modal
      title="Remove Dependency"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid, danger: true }}
      okText="Remove"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Alert
        type="warning"
        showIcon
        title="Only for a dependency that was never true"
        description={`This deletes the record that ${dependency.product.name} depended on ${dependency.dependsOnProduct.name}. If the dependency was real and has stopped, end it instead, so the history of when it held is kept.`}
        style={{ marginBottom: 16 }}
      />
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="remove-product-dependency-form"
      >
        <Item
          name="reason"
          label="Reason"
          rules={[
            {
              required: true,
              message: 'Say why the dependency was never true',
            },
            {
              max: 1024,
              message: 'Reason cannot be longer than 1024 characters',
            },
          ]}
          extra="Recorded in both products' activity."
        >
          <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default RemoveProductDependencyForm
