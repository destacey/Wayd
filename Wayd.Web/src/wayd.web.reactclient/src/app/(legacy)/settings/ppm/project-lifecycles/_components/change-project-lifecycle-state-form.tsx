'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ProjectLifecycleDetailsDto } from '@/src/services/wayd-api'
import {
  useActivateProjectLifecycleMutation,
  useArchiveProjectLifecycleMutation,
} from '@/src/store/features/ppm/project-lifecycles-api'
import { Modal, Space } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export enum ProjectLifecycleStateAction {
  Activate = 'Activate',
  Archive = 'Archive',
}

export interface ChangeProjectLifecycleStateFormProps {
  lifecycle: ProjectLifecycleDetailsDto
  stateAction: ProjectLifecycleStateAction
  onFormComplete: () => void
  onFormCancel: () => void
}

const ChangeProjectLifecycleStateForm = ({
  lifecycle,
  stateAction,
  onFormComplete,
  onFormCancel,
}: ChangeProjectLifecycleStateFormProps) => {
  const messageApi = useMessage()

  const [activateProjectLifecycleMutation] =
    useActivateProjectLifecycleMutation()
  const [archiveProjectLifecycleMutation] =
    useArchiveProjectLifecycleMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        let response = null
        if (stateAction === ProjectLifecycleStateAction.Activate) {
          response = await activateProjectLifecycleMutation(lifecycle.id)
        } else if (stateAction === ProjectLifecycleStateAction.Archive) {
          response = await archiveProjectLifecycleMutation(lifecycle.id)
        }

        if (response?.error) {
          throw response.error
        }

        const pastTense = stateAction === ProjectLifecycleStateAction.Activate ? 'activated' : 'archived'
        messageApi.success(
          `Successfully ${pastTense} project lifecycle.`,
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        const gerund = stateAction === ProjectLifecycleStateAction.Activate ? 'activating' : 'archiving'
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${gerund} the project lifecycle.`,
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${stateAction}ing the project lifecycle.`,
    permission: 'Permissions.ProjectLifecycles.Update',
  })

  return (
    <Modal
      title={`Are you sure you want to ${stateAction} this Project Lifecycle?`}
      open={isOpen}
      onOk={handleOk}
      okText={stateAction}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Space vertical>
        <div>
          {lifecycle?.key} - {lifecycle?.name}
        </div>
        {'This action cannot be undone.'}
      </Space>
    </Modal>
  )
}

export default ChangeProjectLifecycleStateForm
