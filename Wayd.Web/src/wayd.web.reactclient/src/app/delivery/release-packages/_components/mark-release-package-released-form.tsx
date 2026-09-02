'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  MarkReleasePackageReleasedRequest,
  ReleasePackageDto,
} from '@/src/services/wayd-api'
import { useMarkReleasePackageReleasedMutation } from '@/src/store/features/delivery/release-packages-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal } from 'antd'
import { Dayjs } from 'dayjs'

const { Item } = Form

export interface MarkReleasePackageReleasedFormProps {
  releasePackage: ReleasePackageDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface MarkReleasePackageReleasedFormValues {
  releasedDate: Dayjs
}

/**
 * Records that a package shipped.
 *
 * The date is supplied rather than taken from the clock, because shipping is usually recorded after
 * the fact. Releasing closes the manifest — the domain refuses an amendment afterwards.
 */
const MarkReleasePackageReleasedForm = ({
  releasePackage,
  onFormComplete,
  onFormCancel,
}: MarkReleasePackageReleasedFormProps) => {
  const messageApi = useMessage()

  const [markReleased] = useMarkReleasePackageReleasedMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<MarkReleasePackageReleasedFormValues>({
      onSubmit: async (values: MarkReleasePackageReleasedFormValues, form) => {
        try {
          const request = {
            releasedDate: values.releasedDate.format('YYYY-MM-DD'),
          } as unknown as MarkReleasePackageReleasedRequest

          const response = await markReleased({
            id: releasePackage.id,
            cacheKey: releasePackage.key,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Package marked as released.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while releasing the package. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while releasing the package. Please try again.',
      permission: 'Permissions.Delivery.Update',
    })

  return (
    <Modal
      title="Mark Package Released"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Mark Released"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="mark-release-package-released-form"
      >
        <Item
          label="Released Date"
          name="releasedDate"
          rules={[{ required: true, message: 'Released date is required' }]}
          extra={`The manifest for ${releasePackage.version} closes once this is recorded.`}
        >
          <DatePicker style={{ width: '100%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default MarkReleasePackageReleasedForm
