'use client'

import { requiresReason } from '@/src/components/common/status-workflows'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProjectDeliverableDto } from '@/src/services/wayd-api'
import {
  useChangeProjectDeliverableStatusMutation,
  useGetProjectDeliverableStatusOptionsQuery,
} from '@/src/store/features/ppm/project-deliverables-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Alert, Form, Input, Modal, Select } from 'antd'
import { deliverableErrorMessage } from './project-deliverable-errors'

const { Item } = Form
const { TextArea } = Input

export interface ChangeProjectDeliverableStatusFormProps {
  projectId: string
  /** The project's revision when the form opened; a project changed since refuses the move. */
  projectRevision: number
  deliverable: ProjectDeliverableDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeStatusFormValues {
  statusId: string
  reason?: string
}

/**
 * Moves a deliverable to another status of the deliverable workflow. The
 * statuses come from the workflow, in its order, and each says whether the
 * move needs a reason — descoping always does — so the reason is required only
 * once such a status is chosen.
 */
const ChangeProjectDeliverableStatusForm = ({
  projectId,
  projectRevision,
  deliverable,
  onFormComplete,
  onFormCancel,
}: ChangeProjectDeliverableStatusFormProps) => {
  const messageApi = useMessage()
  const [changeStatus] = useChangeProjectDeliverableStatusMutation()
  const {
    data: options,
    isLoading,
    isError,
  } = useGetProjectDeliverableStatusOptionsQuery({
    projectId,
    deliverableId: deliverable.id,
  })

  const needsReason = (statusId: string | undefined) =>
    requiresReason(options?.find((o) => o.id === statusId)?.conditions)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeStatusFormValues>({
      permission: 'Permissions.Projects.Update',
      onSubmit: async (values, form) => {
        try {
          await changeStatus({
            projectId,
            deliverableId: deliverable.id,
            request: {
              statusId: values.statusId,
              reason: values.reason?.trim() || undefined,
              expectedRevision: projectRevision,
            },
          }).unwrap()
          messageApi.success('Status changed.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              deliverableErrorMessage(apiError, 'Failed to change the status.'),
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to change the status.',
    })

  return (
    <Modal
      title={`Change Status · ${deliverable.name}`}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Change"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          title="The statuses this deliverable can move to could not be loaded."
        />
      )}
      {options?.length === 0 && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          title="No status changes are available."
        />
      )}
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-project-deliverable-status-form"
      >
        <Item
          name="statusId"
          label="Status"
          extra={`Currently ${deliverable.status.name}.`}
          rules={[{ required: true, message: 'Choose a status.' }]}
        >
          <Select
            placeholder="Select a status"
            loading={isLoading}
            options={(options ?? []).map((o) => ({
              value: o.id,
              label: o.name,
            }))}
          />
        </Item>
        <Item
          noStyle
          shouldUpdate={(previous, current) =>
            previous.statusId !== current.statusId
          }
        >
          {({ getFieldValue }) => {
            const required = needsReason(getFieldValue('statusId'))
            return (
              /* Revalidated when the status changes, so an error for a status that needed a reason clears with it. */
              <Item
                name="reason"
                label="Reason"
                dependencies={['statusId']}
                extra={
                  required
                    ? 'Required for this status. Recorded in the status history.'
                    : 'Recorded in the status history.'
                }
                rules={[
                  {
                    required,
                    whitespace: true,
                    message: 'A reason is required for this status.',
                  },
                  {
                    max: 1024,
                    message: 'Keep the reason under 1024 characters.',
                  },
                ]}
              >
                <TextArea
                  autoSize={{ minRows: 2 }}
                  showCount
                  maxLength={1024}
                />
              </Item>
            )
          }}
        </Item>
      </Form>
    </Modal>
  )
}

export default ChangeProjectDeliverableStatusForm
