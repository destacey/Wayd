'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { StatusWorkflowDetailsDto } from '@/src/services/wayd-api'
import { usePublishStatusWorkflowMutation } from '@/src/store/features/common/status-workflows-api'
import { Alert, Flex, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

const { Text } = Typography

export interface PublishStatusWorkflowFormProps {
  statusWorkflow: StatusWorkflowDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * The server refuses a publish that is missing a required alias, so they are
 * named here and OK stays disabled rather than surfacing it as an error toast.
 */
const PublishStatusWorkflowForm = ({
  statusWorkflow,
  onFormComplete,
  onFormCancel,
}: PublishStatusWorkflowFormProps) => {
  const messageApi = useMessage()

  const [publishStatusWorkflow] = usePublishStatusWorkflowMutation()

  const missingAliases = statusWorkflow.missingRequiredAliases ?? []
  const hasMissingAliases = missingAliases.length > 0

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await publishStatusWorkflow(statusWorkflow.id)
        if (response?.error) {
          throw response.error
        }
        messageApi.success('Successfully published status workflow.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while publishing the status workflow.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while publishing the status workflow.',
    permission: 'Permissions.StatusWorkflows.Update',
  })

  return (
    <Modal
      title="Publish this Status Workflow?"
      open={isOpen}
      onOk={handleOk}
      okText="Publish"
      okButtonProps={{ disabled: hasMissingAliases }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={12}>
        <Text>
          {statusWorkflow.key} - {statusWorkflow.name}
        </Text>
        {hasMissingAliases ? (
          <Alert
            type="error"
            showIcon
            title="Required meanings are not covered"
            description={
              <>
                <div>
                  Assign a status to each of these before publishing:
                </div>
                <ul style={{ marginTop: 8, marginBottom: 0 }}>
                  {missingAliases.map((alias) => (
                    <li key={alias}>{alias}</li>
                  ))}
                </ul>
              </>
            }
          />
        ) : (
          <Text type="secondary">
            Publishing locks the workflow&apos;s statuses so records can be
            assigned to it. To change it afterwards, clone it into a new draft.
          </Text>
        )}
      </Flex>
    </Modal>
  )
}

export default PublishStatusWorkflowForm
