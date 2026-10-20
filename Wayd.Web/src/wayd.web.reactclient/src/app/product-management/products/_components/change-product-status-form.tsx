'use client'

import { requiresReason } from '@/src/components/common/status-workflows'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ChangeProductStatusRequest, ProductDto } from '@/src/services/wayd-api'
import {
  useChangeProductStatusMutation,
  useGetProductStatusOptionsQuery,
} from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, Form, Input, Modal, Select } from 'antd'
import { useState } from 'react'

const { Item } = Form
const { TextArea } = Input

export interface ChangeProductStatusFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeProductStatusFormValues {
  statusId: string
  reason?: string
}

/**
 * Moves a product to another status in the workflow governing it.
 *
 * The options come from that workflow rather than a fixed list: statuses are
 * configurable, and the API refuses one belonging to a different workflow. Only
 * the moves the workflow allows from the product's current status are offered,
 * in the workflow's own order, which is how an administrator laid the lifecycle
 * out. A reason is asked for only when the chosen move requires one.
 */
const ChangeProductStatusForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: ChangeProductStatusFormProps) => {
  const messageApi = useMessage()

  const [changeProductStatus] = useChangeProductStatusMutation()
  const {
    data: statusOptions,
    isLoading,
    isError,
  } = useGetProductStatusOptionsQuery(product.id)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeProductStatusFormValues>({
      onSubmit: async (values: ChangeProductStatusFormValues, form) => {
        try {
          const request: ChangeProductStatusRequest = {
            id: product.id,
            statusId: values.statusId,
            reason: needsReason ? values.reason?.trim() : undefined,
          }
          const response = await changeProductStatus({
            id: product.id,
            request,
          })
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

  const [statusId, setStatusId] = useState<string>()

  // The current status is excluded: moving to it is a no-op the aggregate
  // ignores, so offering it would promise a change that never happens.
  const available = (statusOptions ?? []).filter(
    (status) => status.id !== product.status?.id,
  )
  const options = available.map((status) => ({
    value: status.id,
    label: status.name,
  }))
  const needsReason = requiresReason(
    available.find((status) => status.id === statusId)?.conditions,
  )

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
      {isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          title="The statuses this product can move to could not be loaded."
        />
      )}
      {!isLoading && !isError && options.length === 0 && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          title={`The workflow allows no move from ${product.status?.name}.`}
        />
      )}
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
            showSearch={{ optionFilterProp: 'label' }}
            onChange={setStatusId}
          />
        </Item>
        {needsReason && (
          <Item
            label="Reason"
            name="reason"
            extra="The workflow requires a reason for this move."
            rules={[
              {
                required: true,
                whitespace: true,
                message: 'A reason is required for this move',
              },
              {
                max: 1024,
                message: 'Reason cannot be longer than 1024 characters',
              },
            ]}
          >
            <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
          </Item>
        )}      </Form>
    </Modal>
  )
}

export default ChangeProductStatusForm
