'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ReleasePackageDto,
  WithdrawReleasePackageRequest,
} from '@/src/services/wayd-api'
import { useWithdrawReleasePackageMutation } from '@/src/store/features/product-management/release-packages-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'

const { Item } = Form
const { TextArea } = Input

export interface WithdrawReleasePackageFormProps {
  releasePackage: ReleasePackageDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface WithdrawReleasePackageFormValues {
  reason?: string
}

/**
 * Pulls a package, with an optional reason recorded on the status transition.
 *
 * The package itself is kept rather than deleted — deployments reference it, and erasing it would
 * take the record of what reached an environment with it.
 */
const WithdrawReleasePackageForm = ({
  releasePackage,
  onFormComplete,
  onFormCancel,
}: WithdrawReleasePackageFormProps) => {
  const messageApi = useMessage()

  const [withdrawPackage] = useWithdrawReleasePackageMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<WithdrawReleasePackageFormValues>({
      onSubmit: async (values: WithdrawReleasePackageFormValues, form) => {
        try {
          const request = {
            reason: values.reason,
          } as WithdrawReleasePackageRequest

          const response = await withdrawPackage({
            id: releasePackage.id,
            cacheKey: releasePackage.key,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Package withdrawn.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while withdrawing the package. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while withdrawing the package. Please try again.',
      permission: 'Permissions.Delivery.Update',
    })

  return (
    <Modal
      title="Withdraw Package"
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
        name="withdraw-release-package-form"
      >
        <Item
          label="Reason"
          name="reason"
          rules={[{ max: 1024, message: 'Reason cannot be longer than 1024 characters' }]}
          extra={`Recorded against ${releasePackage.version} in its status history. The package is kept — deployments may reference it.`}
        >
          <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default WithdrawReleasePackageForm
