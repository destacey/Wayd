'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ReleaseDto, WithdrawReleaseRequest } from '@/src/services/wayd-api'
import { useWithdrawReleaseMutation } from '@/src/store/features/product-management/releases-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, Flex, Form, Input, Modal } from 'antd'

const { Item } = Form
const { TextArea } = Input

export interface WithdrawReleaseFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface WithdrawReleaseFormValues {
  reason?: string
}

/**
 * Retracts a release, with an optional reason recorded on the status transition.
 *
 * Says nothing about the versions the release carried. An artifact that shipped has shipped whatever
 * the market was later told, so a version that was itself pulled is withdrawn separately.
 */
const WithdrawReleaseForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: WithdrawReleaseFormProps) => {
  const messageApi = useMessage()

  const [withdrawRelease] = useWithdrawReleaseMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<WithdrawReleaseFormValues>({
      onSubmit: async (values: WithdrawReleaseFormValues, form) => {
        try {
          const request = { reason: values.reason } as WithdrawReleaseRequest

          const response = await withdrawRelease({
            id: release.id,
            cacheKey: release.key,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Release withdrawn.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while withdrawing the release. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while withdrawing the release. Please try again.',
      permission: 'Permissions.Releases.Update',
    })

  return (
    <Modal
      title="Withdraw Release"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid, danger: true }}
      okText="Withdraw"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Flex vertical gap={16}>
        <Alert
          type="warning"
          showIcon
          title="This retracts the announcement, not the shipment."
          description="Anything this release carried stays as it is. A version that was itself pulled is withdrawn on its own record."
        />
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="withdraw-release-form"
        >
          <Item
            label="Reason"
            name="reason"
            rules={[
              { max: 1024, message: 'Reason cannot be longer than 1024 characters' },
            ]}
            extra={`Recorded against ${release.version} in its status history.`}
          >
            <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
          </Item>
        </Form>
      </Flex>
    </Modal>
  )
}

export default WithdrawReleaseForm
