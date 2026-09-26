'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  CorrectReleasePackageDatesRequest,
  ReleasePackageDto,
} from '@/src/services/wayd-api'
import { useCorrectReleasePackageDatesMutation } from '@/src/store/features/product-management/release-packages-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Flex, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'

const { Item } = Form

export interface CorrectReleasePackageDatesFormProps {
  releasePackage: ReleasePackageDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CorrectReleasePackageDatesFormValues {
  targetDate?: Dayjs
  releasedDate?: Dayjs
}

/**
 * Fixes a package's recorded target and released dates.
 *
 * Separate from Mark Released, which asserts the package shipped and refuses to run twice. This says
 * only that a date was written down wrongly, so the status stays where it is.
 *
 * The released date is offered only on a released package, and cannot be emptied there: a recorded
 * released date is what closes the manifest, so the domain refuses both adding one here and clearing
 * one. Mark Released records the first.
 */
const CorrectReleasePackageDatesForm = ({
  releasePackage,
  onFormComplete,
  onFormCancel,
}: CorrectReleasePackageDatesFormProps) => {
  const messageApi = useMessage()

  const [correctDates] = useCorrectReleasePackageDatesMutation()

  const isReleased = !!releasePackage.releasedDate

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CorrectReleasePackageDatesFormValues>({
      onSubmit: async (values: CorrectReleasePackageDatesFormValues, form) => {
        try {
          // Both dates are sent, so an empty target date is cleared rather than left alone.
          const request = {
            targetDate: values.targetDate?.format('YYYY-MM-DD'),
            releasedDate: values.releasedDate?.format('YYYY-MM-DD'),
          } as unknown as CorrectReleasePackageDatesRequest

          const response = await correctDates({
            id: releasePackage.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Package dates corrected successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while correcting the package dates. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while correcting the package dates. Please try again.',
      permission: 'Permissions.Delivery.Update',
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
          description={`${releasePackage.version} keeps its current status and its history is left as it is.`}
        />
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="correct-release-package-dates-form"
          initialValues={{
            targetDate: releasePackage.targetDate
              ? dayjs(releasePackage.targetDate)
              : undefined,
            releasedDate: releasePackage.releasedDate
              ? dayjs(releasePackage.releasedDate)
              : undefined,
          }}
        >
          <Item
            label="Target Date"
            name="targetDate"
            extra="When the package was aimed at. Clear it to remove the target."
          >
            <DatePicker style={{ width: '100%' }} />
          </Item>
          {isReleased && (
            <Item
              label="Released Date"
              name="releasedDate"
              extra="The local date it shipped."
              rules={[
                {
                  required: true,
                  message: 'A released package keeps its released date.',
                },
              ]}
            >
              <DatePicker style={{ width: '100%' }} />
            </Item>
          )}
        </Form>
      </Flex>
    </Modal>
  )
}

export default CorrectReleasePackageDatesForm
