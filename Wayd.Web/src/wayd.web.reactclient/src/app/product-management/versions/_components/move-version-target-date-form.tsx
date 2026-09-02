'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { MoveVersionTargetDateRequest, VersionDto } from '@/src/services/wayd-api'
import { useMoveVersionTargetDateMutation } from '@/src/store/features/product-management/versions-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'

const { Item } = Form

export interface MoveVersionTargetDateFormProps {
  version: VersionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface MoveVersionTargetDateFormValues {
  targetDate?: Dayjs | null
}

/**
 * Moves or clears a version's target date.
 *
 * Clearing is deliberate rather than a side effect of leaving the field empty: the endpoint reads a
 * null target date as "no longer targeted", which is a different statement from never having set one.
 */
const MoveVersionTargetDateForm = ({
  version,
  onFormComplete,
  onFormCancel,
}: MoveVersionTargetDateFormProps) => {
  const messageApi = useMessage()

  const [moveTargetDate] = useMoveVersionTargetDateMutation()

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<MoveVersionTargetDateFormValues>({
      onSubmit: async (values: MoveVersionTargetDateFormValues, form) => {
        try {
          const request = {
            id: version.id,
            targetDate: values.targetDate
              ? values.targetDate.format('YYYY-MM-DD')
              : undefined,
          } as unknown as MoveVersionTargetDateRequest

          const response = await moveTargetDate({ id: version.id, cacheKey: version.key, request })
          if (response.error) throw response.error

          messageApi.success(
            values.targetDate ? 'Target date moved.' : 'Target date cleared.',
          )
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while moving the target date. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while moving the target date. Please try again.',
      permission: 'Permissions.Delivery.Update',
    })

  return (
    <Modal
      title="Move Target Date"
      open={isOpen}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="move-version-target-date-form"
        initialValues={{
          targetDate: version.targetDate ? dayjs(version.targetDate) : null,
        }}
      >
        <Item
          label="Target Date"
          name="targetDate"
          extra="Clear the date to record that this version is no longer targeted."
        >
          <DatePicker style={{ width: '100%' }} allowClear />
        </Item>
      </Form>
    </Modal>
  )
}

export default MoveVersionTargetDateForm
