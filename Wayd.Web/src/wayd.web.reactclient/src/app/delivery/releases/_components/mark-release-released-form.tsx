'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { MarkReleaseReleasedRequest, ReleaseDto } from '@/src/services/wayd-api'
import { useMarkReleaseReleasedMutation } from '@/src/store/features/delivery/releases-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'

const { Item } = Form

export interface MarkReleaseReleasedFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface MarkReleaseReleasedFormValues {
  releasedDate: Dayjs
}

/**
 * Records the date a release shipped.
 *
 * The picker is floored at the cut date because the aggregate refuses an earlier one — better an
 * unselectable day than a rejected submit.
 */
const MarkReleaseReleasedForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: MarkReleaseReleasedFormProps) => {
  const messageApi = useMessage()

  const [markReleased] = useMarkReleaseReleasedMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<MarkReleaseReleasedFormValues>({
      onSubmit: async (values: MarkReleaseReleasedFormValues, form) => {
        try {
          const request = {
            id: release.id,
            releasedDate: values.releasedDate.format('YYYY-MM-DD'),
          } as unknown as MarkReleaseReleasedRequest

          const response = await markReleased({ id: release.id, cacheKey: release.key, request })
          if (response.error) throw response.error

          messageApi.success('Release marked as released.')
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
      permission: 'Permissions.Releases.Update',
    })

  const cutDate = release.cutDate ? dayjs(release.cutDate) : null

  return (
    <Modal
      title="Mark Released"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Release"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="mark-release-released-form"
      >
        <Item
          label="Released Date"
          name="releasedDate"
          rules={[{ required: true, message: 'Released date is required' }]}
          extra={
            cutDate
              ? `${release.version} was cut on ${cutDate.format('MMM D, YYYY')}.`
              : `${release.version} has not been cut.`
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

export default MarkReleaseReleasedForm
