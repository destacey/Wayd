'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CorrectReleaseDatesRequest, ReleaseDto } from '@/src/services/wayd-api'
import { useCorrectReleaseDatesMutation } from '@/src/store/features/product-management/releases-api'
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
  targetDate?: Dayjs
  releasedDate?: Dayjs
}

/**
 * Fixes a release's recorded target and announced dates.
 *
 * Separate from Mark Released, which asserts the release moved and refuses to run twice. This says
 * only that a date was written down wrongly, so the status stays where it is — the alternative was to
 * withdraw the release and announce it again, which writes two status changes that never happened.
 *
 * Two dates, not three: a release is never cut, so there is no cut date to correct.
 *
 * The announced date can be corrected but not emptied, because an announced record with no announced
 * date contradicts its own status. Reverting is the action for that.
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
          // Both dates are sent, so one left empty is cleared rather than left alone.
          const request = {
            targetDate: values.targetDate?.format('YYYY-MM-DD'),
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
            targetDate: release.targetDate ? dayjs(release.targetDate) : undefined,
            releasedDate: release.releasedDate
              ? dayjs(release.releasedDate)
              : undefined,
          }}
        >
          <Item
            label="Target Date"
            name="targetDate"
            extra="When the release was aimed at. Clear it to remove the target."
          >
            <DatePicker style={{ width: '100%' }} />
          </Item>
          <Item
            label="Announced Date"
            name="releasedDate"
            // Required only once the release has one: the aggregate refuses to clear an announced
            // date, because the status would then contradict the dates.
            rules={
              release.releasedDate
                ? [
                    {
                      required: true,
                      message:
                        'An announced release keeps its announced date. Revert the release instead.',
                    },
                  ]
                : undefined
            }
          >
            <DatePicker style={{ width: '100%' }} />
          </Item>
        </Form>
      </Flex>
    </Modal>
  )
}

export default CorrectReleaseDatesForm
