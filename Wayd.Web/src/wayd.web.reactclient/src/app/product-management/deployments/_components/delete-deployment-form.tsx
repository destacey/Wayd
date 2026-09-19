'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { DeploymentDto } from '@/src/services/wayd-api'
import { useDeleteDeploymentMutation } from '@/src/store/features/product-management/deployments-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Alert, Modal, Typography } from 'antd'

export interface DeleteDeploymentFormProps {
  deployment: DeploymentDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Confirms deleting a deployment. Says what goes with it, because the delivery
 * measures silently change once it is gone — a deployment that really failed or
 * was rolled back should be recorded as such, not deleted.
 */
const DeleteDeploymentForm = ({
  deployment,
  onFormComplete,
  onFormCancel,
}: DeleteDeploymentFormProps) => {
  const messageApi = useMessage()

  const [deleteDeployment] = useDeleteDeploymentMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteDeployment(deployment.id)
        if (response.error) throw response.error

        messageApi.success('Deployment deleted successfully.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while deleting the deployment. Please try again.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while deleting the deployment. Please try again.',
    permission: 'Permissions.Delivery.Delete',
  })

  return (
    <Modal
      title="Delete Deployment"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Typography.Paragraph>
        Delete{' '}
        <Typography.Text strong>Deployment {deployment.key}</Typography.Text>?
      </Typography.Paragraph>
      <Alert
        type="warning"
        showIcon
        title="This cannot be undone"
        description="Its status history is deleted too, and the delivery measures and rollout stop counting it."
      />
      <Typography.Paragraph
        type="secondary"
        style={{ marginTop: 12, marginBottom: 0 }}
      >
        If it failed or was rolled back, record that instead.
      </Typography.Paragraph>
    </Modal>
  )
}

export default DeleteDeploymentForm
