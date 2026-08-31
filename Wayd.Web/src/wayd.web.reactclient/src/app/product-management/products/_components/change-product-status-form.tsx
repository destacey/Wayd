'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ChangeProductStatusRequest, ProductDto } from '@/src/services/wayd-api'
import {
  useChangeProductStatusMutation,
  useGetProductStatusOptionsQuery,
} from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal, Select } from 'antd'

const { Item } = Form

export interface ChangeProductStatusFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeProductStatusFormValues {
  statusId: string
}

/**
 * Moves a product to another status in the workflow governing it.
 *
 * The options come from that workflow rather than a fixed list: statuses are
 * configurable, and the API refuses one belonging to a different workflow. They
 * are offered in the workflow's own order, which is how an administrator laid
 * the lifecycle out.
 */
const ChangeProductStatusForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: ChangeProductStatusFormProps) => {
  const messageApi = useMessage()

  const [changeProductStatus] = useChangeProductStatusMutation()
  const { data: statusOptions, isLoading } = useGetProductStatusOptionsQuery()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeProductStatusFormValues>({
      onSubmit: async (values: ChangeProductStatusFormValues, form) => {
        try {
          const request = { id: product.id, statusId: values.statusId } as ChangeProductStatusRequest
          const response = await changeProductStatus({ id: product.id, request })
          if (response.error) throw response.error

          messageApi.success('Product status changed successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while changing the status. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while changing the status. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  // The current status is excluded: moving to it is a no-op the aggregate
  // ignores, so offering it would promise a change that never happens.
  const options = (statusOptions ?? [])
    .filter((status) => status.id !== product.status?.id)
    .map((status) => ({ value: status.id, label: status.name }))

  return (
    <Modal
      title="Change Status"
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
        name="change-product-status-form"
      >
        <Item
          label="Status"
          name="statusId"
          rules={[{ required: true, message: 'Status is required' }]}
          extra={`Currently ${product.status?.name}.`}
        >
          <Select
            options={options}
            loading={isLoading}
            placeholder="Select a status"
            showSearch
            optionFilterProp="label"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ChangeProductStatusForm
