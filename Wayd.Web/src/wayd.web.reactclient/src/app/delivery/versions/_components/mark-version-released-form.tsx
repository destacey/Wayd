'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { MarkVersionReleasedRequest, VersionDto } from '@/src/services/wayd-api'
import { useMarkVersionReleasedMutation } from '@/src/store/features/delivery/versions-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'

const { Item } = Form

export interface MarkVersionReleasedFormProps {
  version: VersionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface MarkVersionReleasedFormValues {
  releasedDate: Dayjs
}

/**
 * Records the date a version shipped.
 *
 * The picker is floored at the cut date because the aggregate refuses an earlier one — better an
 * unselectable day than a rejected submit.
 */
const MarkVersionReleasedForm = ({
  version,
  onFormComplete,
  onFormCancel,
}: MarkVersionReleasedFormProps) => {
  const messageApi = useMessage()

  const [markReleased] = useMarkVersionReleasedMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<MarkVersionReleasedFormValues>({
      onSubmit: async (values: MarkVersionReleasedFormValues, form) => {
        try {
          const request = {
            id: version.id,
            releasedDate: values.releasedDate.format('YYYY-MM-DD'),
          } as unknown as MarkVersionReleasedRequest

          const response = await markReleased({ id: version.id, cacheKey: version.key, request })
          if (response.error) throw response.error

          messageApi.success('Version marked as released.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while releasing. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while releasing. Please try again.',
      permission: 'Permissions.Delivery.Update',
    })

  const cutDate = version.cutDate ? dayjs(version.cutDate) : null

  return (
    <Modal
      title="Mark Released"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Version"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="mark-version-released-form"
      >
        <Item
          label="Released Date"
          name="releasedDate"
          rules={[{ required: true, message: 'Released date is required' }]}
          extra={
            cutDate
              ? `${version.number} was cut on ${cutDate.format('MMM D, YYYY')}.`
              : `${version.number} has not been cut.`
          }
        >
          <DatePicker
            style={{ width: '100%' }}
            disabledDate={
              cutDate ? (current) => current.isBefore(cutDate, 'day') : undefined
            }
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default MarkVersionReleasedForm
