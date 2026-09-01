'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CutReleaseRequest, ReleaseDto } from '@/src/services/wayd-api'
import { useCutReleaseMutation } from '@/src/store/features/delivery/releases-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal } from 'antd'
import { Dayjs } from 'dayjs'

const { Item } = Form

export interface CutReleaseFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CutReleaseFormValues {
  cutDate: Dayjs
}

/**
 * Records the date a release was cut.
 *
 * One-way: the aggregate refuses a second cut, and refuses one on a release already released or
 * withdrawn. The caller decides whether to offer this at all.
 */
const CutReleaseForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: CutReleaseFormProps) => {
  const messageApi = useMessage()

  const [cutRelease] = useCutReleaseMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CutReleaseFormValues>({
      onSubmit: async (values: CutReleaseFormValues, form) => {
        try {
          const request = {
            id: release.id,
            cutDate: values.cutDate.format('YYYY-MM-DD'),
          } as unknown as CutReleaseRequest

          const response = await cutRelease({ id: release.id, request })
          if (response.error) throw response.error

          messageApi.success('Release cut successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while cutting the release. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while cutting the release. Please try again.',
      permission: 'Permissions.Releases.Update',
    })

  return (
    <Modal
      title="Cut Release"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Cut"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="cut-release-form">
        <Item
          label="Cut Date"
          name="cutDate"
          rules={[{ required: true, message: 'Cut date is required' }]}
          extra={`Cutting ${release.version} is one-way — it cannot be undone.`}
        >
          <DatePicker style={{ width: '100%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default CutReleaseForm
