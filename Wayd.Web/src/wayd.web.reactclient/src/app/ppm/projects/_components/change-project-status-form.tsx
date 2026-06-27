'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import {
  useApproveProjectMutation,
  useActivateProjectMutation,
  useCancelProjectMutation,
  useCompleteProjectMutation,
} from '@/src/store/features/ppm/projects-api'
import { Alert, Modal, Space } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export enum ProjectStatusAction {
  Approve = 'Approve',
  Activate = 'Activate',
  Complete = 'Complete',
  Cancel = 'Cancel',
}

const statusActionToPastTense = (statusAction: ProjectStatusAction) => {
  switch (statusAction) {
    case ProjectStatusAction.Approve:
      return 'approved'
    case ProjectStatusAction.Activate:
      return 'activated'
    case ProjectStatusAction.Complete:
      return 'completed'
    case ProjectStatusAction.Cancel:
      return 'cancelled'
    default:
      return statusAction
  }
}

const statusActionToPresentTense = (statusAction: ProjectStatusAction) => {
  switch (statusAction) {
    case ProjectStatusAction.Approve:
      return 'approving'
    case ProjectStatusAction.Activate:
      return 'activating'
    case ProjectStatusAction.Complete:
      return 'completing'
    case ProjectStatusAction.Cancel:
      return 'cancelling'
    default:
      return statusAction
  }
}

export interface ChangeProjectStatusFormProps {
  project: ProjectDetailsDto
  statusAction: ProjectStatusAction
  onFormComplete: () => void
  onFormCancel: () => void
}

const ChangeProjectStatusForm = ({
  project,
  statusAction,
  onFormComplete,
  onFormCancel,
}: ChangeProjectStatusFormProps) => {
  const messageApi = useMessage()

  const [approveProjectMutation] = useApproveProjectMutation()
  const [activateProjectMutation] = useActivateProjectMutation()
  const [completeProjectMutation] = useCompleteProjectMutation()
  const [cancelProjectMutation] = useCancelProjectMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      // Check start/end dates before activating
      if (
        statusAction === ProjectStatusAction.Activate &&
        (!project.start || !project.end)
      ) {
        messageApi.error(
          'Project start and end dates must be set before activating the project.',
        )
        return false
      }

      try {
        const request = { id: project.id, cacheKey: project.key }
        let response = null
        if (statusAction === ProjectStatusAction.Approve) {
          response = await approveProjectMutation(request)
        } else if (statusAction === ProjectStatusAction.Activate) {
          response = await activateProjectMutation(request)
        } else if (statusAction === ProjectStatusAction.Complete) {
          response = await completeProjectMutation(request)
        } else if (statusAction === ProjectStatusAction.Cancel) {
          response = await cancelProjectMutation(request)
        }

        if (response?.error) throw response.error

        messageApi.success(
          `Successfully ${statusActionToPastTense(statusAction)} Project.`,
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${statusActionToPresentTense(statusAction)} the project.`,
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${statusActionToPresentTense(statusAction)} the project.`,
    permission: 'Permissions.Projects.Update',
  })

  return (
    <Modal
      title={`Are you sure you want to ${statusAction} this Project?`}
      open={isOpen}
      onOk={handleOk}
      okText={
        statusAction === ProjectStatusAction.Cancel
          ? 'Cancel Project'
          : statusAction
      }
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Space vertical>
        <div>
          {project?.key} - {project?.name}
        </div>
        <Alert title="This action cannot be undone." type="warning" showIcon />
      </Space>
    </Modal>
  )
}

export default ChangeProjectStatusForm
