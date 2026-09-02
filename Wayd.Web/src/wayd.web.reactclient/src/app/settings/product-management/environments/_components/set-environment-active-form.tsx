'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import {
  DeploymentEnvironmentDto,
  SetDeploymentEnvironmentActiveRequest,
} from '@/src/services/wayd-api'
import { useSetDeploymentEnvironmentActiveMutation } from '@/src/store/features/product-management/deployment-environments-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Alert, Modal, Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface SetEnvironmentActiveFormProps {
  environment: DeploymentEnvironmentDto
  /** True to reinstate, false to retire. */
  isActive: boolean
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Retires an environment, or puts it back.
 *
 * Retiring is the destructive action here — there is no delete, and there should not be: deployments
 * already recorded point at this environment, and removing it would take the record of what reached
 * it with them. A retired environment simply stops being offered as a target.
 *
 * The deployment count is what makes the confirmation meaningful. "Retire QA2" says nothing about
 * consequence; "Retire QA2, which 47 deployments reference" does.
 */
const SetEnvironmentActiveForm = ({
  environment,
  isActive,
  onFormComplete,
  onFormCancel,
}: SetEnvironmentActiveFormProps) => {
  const messageApi = useMessage()

  const [setActive] = useSetDeploymentEnvironmentActiveMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await setActive({
          id: environment.id,
          request: {
            id: environment.id,
            isActive,
          } as SetDeploymentEnvironmentActiveRequest,
        })

        if (response.error) throw response.error

        messageApi.success(
          isActive ? 'Environment reinstated.' : 'Environment retired.',
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An error occurred while ${isActive ? 'reinstating' : 'retiring'} the environment. Please try again.`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An error occurred while ${isActive ? 'reinstating' : 'retiring'} the environment. Please try again.`,
    permission: 'Permissions.DeploymentEnvironments.Update',
  })

  return (
    <Modal
      title={isActive ? 'Reinstate Environment' : 'Retire Environment'}
      open={isOpen}
      onOk={handleOk}
      okText={isActive ? 'Reinstate' : 'Retire'}
      okButtonProps={{ danger: !isActive }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Paragraph>
        {isActive ? 'Reinstate ' : 'Retire '}
        <Text strong>{environment.name}</Text>?
      </Paragraph>
      {isActive ? (
        <Paragraph type="secondary">
          It will be offered as a deployment target again.
        </Paragraph>
      ) : (
        <>
          <Paragraph type="secondary">
            It will no longer be offered as a deployment target. Nothing is
            deleted — the environment and its history are kept.
          </Paragraph>
          {environment.deploymentCount > 0 && (
            <Alert
              type="info"
              showIcon
              title={
                environment.deploymentCount === 1
                  ? '1 deployment references this environment'
                  : `${environment.deploymentCount} deployments reference this environment`
              }
              description="Those records stand, and keep counting toward the measures they already count toward."
            />
          )}
        </>
      )}
    </Modal>
  )
}

export default SetEnvironmentActiveForm
