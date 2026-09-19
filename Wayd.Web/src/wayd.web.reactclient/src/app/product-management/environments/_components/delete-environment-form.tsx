'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { DeploymentEnvironmentDto } from '@/src/services/wayd-api'
import { useDeleteDeploymentEnvironmentMutation } from '@/src/store/features/product-management/deployment-environments-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Alert, Modal, Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface DeleteEnvironmentFormProps {
  environment: DeploymentEnvironmentDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Deletes an environment, and every deployment into it with it.
 *
 * The deployment count is the consequence, so it is spelled out as a warning
 * rather than left for the reader to infer.
 */
const DeleteEnvironmentForm = ({
  environment,
  onFormComplete,
  onFormCancel,
}: DeleteEnvironmentFormProps) => {
  const messageApi = useMessage()

  const [deleteEnvironment] = useDeleteDeploymentEnvironmentMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteEnvironment(environment.id)
        if (response.error) throw response.error

        messageApi.success('Environment deleted.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while deleting the environment. Please try again.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while deleting the environment. Please try again.',
    permission: 'Permissions.DeploymentEnvironments.Delete',
  })

  return (
    <Modal
      title="Delete Environment"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph>
        Delete environment <Text strong>{environment.name}</Text>?
      </Paragraph>
      <Alert
        type="warning"
        showIcon
        title="This cannot be undone"
        description={
          environment.deploymentCount === 0
            ? 'No deployments reference this environment.'
            : environment.deploymentCount === 1
              ? 'The 1 deployment into it is deleted too, with its status history, and the delivery measures and rollout stop counting it.'
              : `The ${environment.deploymentCount} deployments into it are deleted too, with their status history, and the delivery measures and rollout stop counting them.`
        }
      />
      <Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
        Retire it instead to keep its deployments.
      </Paragraph>
    </Modal>
  )
}

export default DeleteEnvironmentForm
