'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { VersionDto, WithdrawVersionRequest } from '@/src/services/wayd-api'
import { useWithdrawVersionMutation } from '@/src/store/features/product-management/versions-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'

const { Item } = Form
const { TextArea } = Input

export interface WithdrawVersionFormProps {
  version: VersionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface WithdrawVersionFormValues {
  reason?: string
}

/**
 * Withdraws a version, with an optional reason recorded on the status transition.
 *
 * Available for a released version as well as an unreleased one: pulling something after it shipped
 * is the case this exists for.
 */
const WithdrawVersionForm = ({
  version,
  onFormComplete,
  onFormCancel,
}: WithdrawVersionFormProps) => {
  const messageApi = useMessage()

  const [withdrawVersion] = useWithdrawVersionMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<WithdrawVersionFormValues>({
      onSubmit: async (values: WithdrawVersionFormValues, form) => {
        try {
          const request = {
            id: version.id,
            reason: values.reason,
          } as WithdrawVersionRequest

          const response = await withdrawVersion({ id: version.id, cacheKey: version.key, request })
          if (response.error) throw response.error

          messageApi.success('Version withdrawn.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while withdrawing the version. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while withdrawing the version. Please try again.',
      permission: 'Permissions.Delivery.Update',
    })

  return (
    <Modal
      title="Withdraw Version"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid, danger: true }}
      okText="Withdraw"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="withdraw-version-form"
      >
        <Item
          label="Reason"
          name="reason"
          rules={[{ max: 1024, message: 'Reason cannot be longer than 1024 characters' }]}
          extra={`Recorded against ${version.number} in its status history.`}
        >
          <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default WithdrawVersionForm
