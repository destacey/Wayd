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
        Delete <Text strong>{environment.name}</Text>? This cannot be undone.
      </Paragraph>
      {environment.deploymentCount > 0 ? (
        <Alert
          type="warning"
          showIcon
          title={
            environment.deploymentCount === 1
              ? '1 deployment into this environment will also be deleted'
              : `${environment.deploymentCount} deployments into this environment will also be deleted`
          }
          description="With their status history. The delivery measures and rollout will stop counting them. Retire the environment instead to keep them."
        />
      ) : (
        <Paragraph type="secondary">
          No deployments reference this environment.
        </Paragraph>
      )}
    </Modal>
  )
}

export default DeleteEnvironmentForm
