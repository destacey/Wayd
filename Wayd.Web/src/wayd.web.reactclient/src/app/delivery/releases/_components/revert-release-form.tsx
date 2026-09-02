'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ReleaseDto, RevertReleaseRequest } from '@/src/services/wayd-api'
import { useRevertReleaseMutation } from '@/src/store/features/delivery/releases-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, Flex, Form, Input, Modal } from 'antd'

const { Item } = Form
const { TextArea } = Input

export interface RevertReleaseFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface RevertReleaseFormValues {
  reason: string
}

/**
 * Records that a release marked as shipped did not in fact ship.
 *
 * Deliberately not a withdrawal. Withdrawing says a real release was pulled and is terminal; this says
 * the release never happened, so it moves back to a live status and can be released properly later.
 * Recording the first as the second would leave the history asserting a withdrawal nobody performed.
 *
 * The released date is cleared as part of the move, because the date and the status are one fact.
 */
const RevertReleaseForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: RevertReleaseFormProps) => {
  const messageApi = useMessage()

  const [revertRelease] = useRevertReleaseMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<RevertReleaseFormValues>({
      onSubmit: async (values: RevertReleaseFormValues, form) => {
        try {
          const request = { reason: values.reason } as RevertReleaseRequest

          const response = await revertRelease({ id: release.id, cacheKey: release.key, request })
          if (response.error) throw response.error

          messageApi.success('Release reverted.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while reverting the release. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while reverting the release. Please try again.',
      permission: 'Permissions.Releases.Update',
    })

  return (
    <Modal
      title="Revert Release"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid, danger: true }}
      okText="Revert"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Flex vertical gap={16}>
        <Alert
          type="warning"
          showIcon
          title="For a release that was marked shipped by mistake."
          description={`${release.version} goes back to ${
            release.cutDate ? 'Ready' : 'its initial status'
          } and its released date is cleared. If it did ship and was then pulled, withdraw it instead.`}
        />
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="revert-release-form"
        >
          <Item
            label="Reason"
            name="reason"
            rules={[
              { required: true, message: 'Reason is required' },
              { max: 1024, message: 'Reason cannot be longer than 1024 characters' },
            ]}
            // Required, unlike a withdrawal's optional reason: this contradicts something the status
            // history already asserts, so the record has to say why.
            extra="Recorded against the status change."
          >
            <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
          </Item>
        </Form>
      </Flex>
    </Modal>
  )
}

export default RevertReleaseForm
