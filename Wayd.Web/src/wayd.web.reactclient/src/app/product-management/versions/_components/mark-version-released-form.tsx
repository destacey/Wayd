'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { MarkVersionReleasedRequest, VersionDto } from '@/src/services/wayd-api'
import { useMarkVersionReleasedMutation } from '@/src/store/features/product-management/versions-api'
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
  releasedAt: Dayjs
}

/**
 * Records the moment a version shipped.
 *
 * The picker is floored at the cut moment because the aggregate refuses an earlier one — better an
 * unselectable day than a rejected submit. disabledDate works a day at a time, so a rule catches an
 * earlier time on the cut day itself.
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
            releasedAt: values.releasedAt.toDate(),
          } as MarkVersionReleasedRequest

          const response = await markReleased({
            id: version.id,
            cacheKey: version.key,
            request,
          })
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

  const cutAt = version.cutAt ? dayjs(version.cutAt) : null

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
          label="Released At"
          name="releasedAt"
          initialValue={dayjs()}
          rules={[
            { required: true, message: 'Released at is required' },
            {
              validator: (_, value: Dayjs | undefined) =>
                !value || !cutAt || !value.isBefore(cutAt)
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error(
                        'A version cannot be released before it was cut',
                      ),
                    ),
            },
          ]}
          extra={
            cutAt
              ? `${version.number} was cut on ${cutAt.format('MMM D, YYYY h:mm A')}.`
              : `${version.number} has not been cut.`
          }
        >
          <DatePicker
            showTime
            style={{ width: '100%' }}
            disabledDate={
              cutAt ? (current) => current.isBefore(cutAt, 'day') : undefined
            }
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default MarkVersionReleasedForm
