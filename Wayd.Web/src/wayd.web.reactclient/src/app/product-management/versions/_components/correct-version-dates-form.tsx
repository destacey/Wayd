'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CorrectVersionDatesRequest, VersionDto } from '@/src/services/wayd-api'
import { useCorrectVersionDatesMutation } from '@/src/store/features/product-management/versions-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Flex, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'

const { Item } = Form

export interface CorrectVersionDatesFormProps {
  version: VersionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CorrectVersionDatesFormValues {
  targetDate?: Dayjs
  cutAt?: Dayjs
  releasedAt?: Dayjs
}

/**
 * Fixes a version's recorded target date and cut and released moments.
 *
 * Separate from Cut and Mark Released, which assert the version moved and refuse to run twice. This
 * says only that a value was written down wrongly, so the status stays where it is — the alternative
 * was to withdraw the version and version it again, which writes two status changes that never
 * happened.
 *
 * Every value is offered whether or not the version has one, because a missing value is as likely to
 * be the error as a wrong one — a version can be marked released without ever being cut, so the cut
 * moment is often filled in afterwards. Clearing the target date or cut moment is allowed for the same
 * reason.
 *
 * The released moment is the exception: it can be corrected but not emptied here, because a released
 * record with no released moment contradicts its own status. Reverting is the action for that.
 */
const CorrectVersionDatesForm = ({
  version,
  onFormComplete,
  onFormCancel,
}: CorrectVersionDatesFormProps) => {
  const messageApi = useMessage()

  const [correctVersionDates] = useCorrectVersionDatesMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CorrectVersionDatesFormValues>({
      onSubmit: async (values: CorrectVersionDatesFormValues, form) => {
        try {
          // Every value is sent, so one left empty is cleared rather than left alone.
          const request = {
            id: version.id,
            targetDate: values.targetDate?.format('YYYY-MM-DD'),
            cutAt: values.cutAt?.toDate(),
            releasedAt: values.releasedAt?.toDate(),
          } as unknown as CorrectVersionDatesRequest

          const response = await correctVersionDates({
            id: version.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Version dates corrected successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while correcting the version dates. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while correcting the version dates. Please try again.',
      permission: 'Permissions.Delivery.Update',
    })

  const cutAt: Dayjs | undefined = Form.useWatch('cutAt', form)

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
          description={`${version.number} keeps its current status and its history is left as it is.`}
        />
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="correct-version-dates-form"
          initialValues={{
            targetDate: version.targetDate
              ? dayjs(version.targetDate)
              : undefined,
            cutAt: version.cutAt ? dayjs(version.cutAt) : undefined,
            releasedAt: version.releasedAt
              ? dayjs(version.releasedAt)
              : undefined,
          }}
        >
          <Item
            label="Target Date"
            name="targetDate"
            extra="When the version was aimed at. Clear it to remove the target."
          >
            <DatePicker style={{ width: '100%' }} />
          </Item>
          <Item
            label="Cut At"
            name="cutAt"
            extra="A version can ship without ever being cut, so this may be filled in afterwards."
          >
            <DatePicker showTime style={{ width: '100%' }} />
          </Item>
          <Item
            label="Released At"
            name="releasedAt"
            dependencies={['cutAt']}
            rules={[
              // Required only once the version has one: the aggregate refuses to clear a released
              // moment, because the status would then contradict the record.
              ...(version.releasedAt
                ? [
                    {
                      required: true,
                      message:
                        'A released version keeps its released moment. Revert the version instead.',
                    },
                  ]
                : []),
              // The aggregate refuses a release before the cut; the picker only disables whole days.
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
      </Flex>
    </Modal>
  )
}

export default CorrectVersionDatesForm
