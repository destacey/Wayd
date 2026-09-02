'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CutVersionRequest, VersionDto } from '@/src/services/wayd-api'
import { useCutVersionMutation } from '@/src/store/features/delivery/versions-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal } from 'antd'
import { Dayjs } from 'dayjs'

const { Item } = Form

export interface CutVersionFormProps {
  version: VersionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CutVersionFormValues {
  cutDate: Dayjs
}

/**
 * Records the date a version was cut.
 *
 * One-way: the aggregate refuses a second cut, and refuses one on a version already released or
 * withdrawn. The caller decides whether to offer this at all.
 */
const CutVersionForm = ({
  version,
  onFormComplete,
  onFormCancel,
}: CutVersionFormProps) => {
  const messageApi = useMessage()

  const [cutVersion] = useCutVersionMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CutVersionFormValues>({
      onSubmit: async (values: CutVersionFormValues, form) => {
        try {
          const request = {
            id: version.id,
            cutDate: values.cutDate.format('YYYY-MM-DD'),
          } as unknown as CutVersionRequest

          const response = await cutVersion({ id: version.id, cacheKey: version.key, request })
          if (response.error) throw response.error

          messageApi.success('Version cut successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while cutting the version. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while cutting the version. Please try again.',
      permission: 'Permissions.Delivery.Update',
    })

  return (
    <Modal
      title="Cut Version"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Cut"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="cut-version-form">
        <Item
          label="Cut Date"
          name="cutDate"
          rules={[{ required: true, message: 'Cut date is required' }]}
          extra={`Cutting ${version.number} is one-way — it cannot be undone.`}
        >
          <DatePicker style={{ width: '100%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default CutVersionForm
