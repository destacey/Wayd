'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { StatusWorkflowDetailsDto } from '@/src/services/wayd-api'
import { useArchiveStatusWorkflowMutation } from '@/src/store/features/common/status-workflows-api'
import { Alert, Flex, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

const { Text } = Typography

export interface ArchiveStatusWorkflowFormProps {
  statusWorkflow: StatusWorkflowDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * An assigned workflow cannot be archived — its records would have nothing to
 * interpret their statuses against. Reassign the scope first.
 */
const ArchiveStatusWorkflowForm = ({
  statusWorkflow,
  onFormComplete,
  onFormCancel,
}: ArchiveStatusWorkflowFormProps) => {
  const messageApi = useMessage()

  const [archiveStatusWorkflow] = useArchiveStatusWorkflowMutation()

  const isBlocked = statusWorkflow.isAssigned

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await archiveStatusWorkflow(statusWorkflow.id)
        if (response?.error) {
          throw response.error
        }
        messageApi.success('Successfully archived status workflow.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while archiving the status workflow.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while archiving the status workflow.',
    permission: 'Permissions.StatusWorkflows.Update',
  })

  return (
    <Modal
      title="Archive this Status Workflow?"
      open={isOpen}
      onOk={handleOk}
      okText="Archive"
      okType="danger"
      okButtonProps={{ disabled: isBlocked }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={12}>
        <Text>
          {statusWorkflow.key} - {statusWorkflow.name}
        </Text>
        {isBlocked ? (
          <Alert
            type="error"
            showIcon
            title="This workflow is still in use"
            description="Records are assigned to it, so archiving would leave their statuses with nothing to interpret them against. Reassign those records to another workflow first."
          />
        ) : (
          <Text type="secondary">
            An archived workflow can no longer be assigned to records. Records
            already using it keep the statuses they hold.
          </Text>
        )}
      </Flex>
    </Modal>
  )
}

export default ArchiveStatusWorkflowForm
