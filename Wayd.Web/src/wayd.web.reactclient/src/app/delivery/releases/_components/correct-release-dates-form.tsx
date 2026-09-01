'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CorrectReleaseDatesRequest, ReleaseDto } from '@/src/services/wayd-api'
import { useCorrectReleaseDatesMutation } from '@/src/store/features/delivery/releases-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Flex, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'

const { Item } = Form

export interface CorrectReleaseDatesFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CorrectReleaseDatesFormValues {
  cutDate?: Dayjs
  releasedDate?: Dayjs
}

/**
 * Fixes a release's recorded cut and released dates.
 *
 * Separate from Cut and Mark Released, which assert the release moved and refuse to run twice. This
 * says only that a date was written down wrongly, so the status stays where it is — the alternative
 * was to withdraw the release and release it again, which writes two status changes that never
 * happened.
 *
 * Only dates the release already has are offered. Adding one is a lifecycle step, and belongs to the
 * action that performs it.
 */
const CorrectReleaseDatesForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: CorrectReleaseDatesFormProps) => {
  const messageApi = useMessage()

  const [correctReleaseDates] = useCorrectReleaseDatesMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CorrectReleaseDatesFormValues>({
      onSubmit: async (values: CorrectReleaseDatesFormValues, form) => {
        try {
          const request = {
            id: release.id,
            cutDate: values.cutDate?.format('YYYY-MM-DD'),
            releasedDate: values.releasedDate?.format('YYYY-MM-DD'),
          } as unknown as CorrectReleaseDatesRequest

          const response = await correctReleaseDates({
            id: release.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Release dates corrected successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while correcting the release dates. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while correcting the release dates. Please try again.',
      permission: 'Permissions.Releases.Update',
    })

  const cutDate = Form.useWatch('cutDate', form)

  return (
    <Modal
      title="Correct Dates"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Flex vertical gap={16}>
        <Alert
          type="info"
          showIcon
          title="Corrects what was recorded, not what happened."
          description={`${release.version} keeps its current status and its history is left as it is.`}
        />
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="correct-release-dates-form"
          initialValues={{
            cutDate: release.cutDate ? dayjs(release.cutDate) : undefined,
            releasedDate: release.releasedDate
              ? dayjs(release.releasedDate)
              : undefined,
          }}
        >
          {release.cutDate && (
            <Item
              label="Cut Date"
              name="cutDate"
              rules={[{ required: true, message: 'Cut date is required' }]}
            >
              <DatePicker style={{ width: '100%' }} />
            </Item>
          )}
          {release.releasedDate && (
            <Item
              label="Released Date"
              name="releasedDate"
              rules={[{ required: true, message: 'Released date is required' }]}
            >
              <DatePicker
                style={{ width: '100%' }}
                // The aggregate refuses a released date before the cut date.
                disabledDate={
                  cutDate
                    ? (current) => current.isBefore(cutDate, 'day')
                    : undefined
                }
              />
            </Item>
          )}
        </Form>
      </Flex>
    </Modal>
  )
}

export default CorrectReleaseDatesForm
